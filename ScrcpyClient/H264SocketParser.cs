using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ScrcpyClient
{
    public unsafe class H264SocketParser
    {
        private Thread threadVideo;

        private SDLHelper sdlvideo;

        public H264SocketParser(SDLHelper sdlvideo)
        {
            this.sdlvideo = sdlvideo;
        }

        public void StartPlay(NetworkStream stream)
        {
            try
            {

                AVCodec* mCodec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
                if (mCodec == null)
                {
                    Console.WriteLine("can not find h264 decoder");
                    return;
                }

                AVCodecContext* mCodecContext = ffmpeg.avcodec_alloc_context3(mCodec);
                if (mCodecContext == null)
                {
                    Console.WriteLine("can not allcote codec context");
                    return;
                }

                if (ffmpeg.avcodec_open2(mCodecContext, mCodec, null) < 0)
                {
                    Console.WriteLine("can not open codec");
                    ffmpeg.avcodec_free_context(&mCodecContext);
                    return;
                }

                AVCodecParserContext* parser = ffmpeg.av_parser_init((int)AVCodecID.AV_CODEC_ID_H264);
                if (parser == null)
                {
                    Console.WriteLine("can not initialize parser");
                    ffmpeg.avcodec_close(mCodecContext);
                    ffmpeg.avcodec_free_context(&mCodecContext);
                    return;
                }

                //AVFrame用于存储解码后的像素数据(YUV)
                //内存分配
                AVFrame* pFrame = ffmpeg.av_frame_alloc();
                //YUV420
                AVFrame* pFrameYUV = ffmpeg.av_frame_alloc();
                //只有指定了AVFrame的像素格式、画面大小才能真正分配内存
                //缓冲区分配内存
                int out_buffer_size = ffmpeg.avpicture_get_size(AVPixelFormat.AV_PIX_FMT_YUV420P, 720, 1280);
                byte* out_buffer = (byte*)ffmpeg.av_malloc((ulong)out_buffer_size);
                //初始化缓冲区
                ffmpeg.avpicture_fill((AVPicture*)pFrameYUV, out_buffer, AVPixelFormat.AV_PIX_FMT_YUV420P, 720, 1280);

                //用于转码（缩放）的参数，转之前的宽高，转之后的宽高，格式等
                //SwsContext* sws_ctx = ffmpeg.sws_getContext(mCodecContext->width, mCodecContext->height, AVPixelFormat.AV_PIX_FMT_YUV420P /*pCodecCtx->pix_fmt*/, mCodecContext->width, mCodecContext->height, AVPixelFormat.AV_PIX_FMT_YUV420P, ffmpeg.SWS_BICUBIC, null, null, null);
                SwsContext* sws_ctx = ffmpeg.sws_getContext(720, 1280, AVPixelFormat.AV_PIX_FMT_YUV420P /*pCodecCtx->pix_fmt*/, 720, 1280, AVPixelFormat.AV_PIX_FMT_YUV420P, ffmpeg.SWS_BICUBIC, null, null, null);


                AVPacket packet;
                ffmpeg.av_init_packet(&packet);

                byte[] inbuffer = new byte[4096];
                int cur_size, ret, got_picture;
                int readLen = 0;
                bool first_time = true;

                sdlvideo.SDL_Init(720, 1280);

                while (true)
                {
                    readLen = 0;
                    cur_size = stream.Read(inbuffer, 0, 4096);
                    if(cur_size <= 0)
                    {
                        Console.WriteLine("read eof");
                        break;
                    }

                    while(cur_size > 0)
                    {
                        fixed (byte* cur_ptr = inbuffer)
                        {
                            int len = ffmpeg.av_parser_parse2(parser, mCodecContext,
                             &packet.data, &packet.size, cur_ptr + readLen, cur_size,
                             ffmpeg.AV_NOPTS_VALUE, ffmpeg.AV_NOPTS_VALUE, -1);

                            readLen += len;
                            cur_size -= len;
                        }

                        if (packet.size == 0) continue;

                        Console.WriteLine($"[Packet]Size:{packet.size}");

                        ret = ffmpeg.avcodec_decode_video2(mCodecContext, pFrame, &got_picture, &packet);
                        if (ret < 0)
                        {
                            Console.WriteLine("Decode Error.\n");
                            break;
                        }
                        // 读取解码后的帧数据
                        if (got_picture > 0)
                        {
                            if (first_time)
                            {
                                Console.WriteLine($"width:{mCodecContext->width}\nheight:{mCodecContext->height}\n\n");
                                first_time = false;
                            }
                            //AVFrame转为像素格式YUV420，宽高
                            ffmpeg.sws_scale(sws_ctx, pFrame->data, pFrame->linesize, 0, mCodecContext->height, pFrameYUV->data, pFrameYUV->linesize);

                            //SDL播放YUV数据
                            var data = out_buffer;
                            sdlvideo.SDL_Display(mCodecContext->width, mCodecContext->height, (IntPtr)data, out_buffer_size, pFrameYUV->linesize[0]);
                        }
                    }
                }

                Console.WriteLine("exit loop");
                ffmpeg.av_parser_close(parser);
                ffmpeg.avcodec_close(mCodecContext);
                ffmpeg.avcodec_free_context(&mCodecContext);
                // todo notify stopped
            }
            catch (Exception ex)
            {
                Console.WriteLine("H264SocketParser.thread error:", ex);
            }
        }

    }
}
