using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ScrcpyClient
{
    public unsafe class JT1078CodecForMp4
    {
        /// <summary>
        /// 指示当前解码是否在运行
        /// </summary>
        public bool IsRun { get; protected set; }
        /// <summary>
        /// 视频线程
        /// </summary>
        private Thread threadVideo;
        /// <summary>
        /// 音频线程
        /// </summary>
        private Thread threadAudio;
        /// <summary>
        /// 退出控制
        /// </summary>
        private bool exit_thread = false;
        /// <summary>
        /// 暂停控制
        /// </summary>
        private bool pause_thread = false;
        /// <summary>
        ///  视频输出流videoindex
        /// </summary>
        private int videoindex = -1;
        /// <summary>
        ///  音频输出流audioindex
        /// </summary>
        private int audioindex = -1;

        /// <summary>
        /// 视频H264转YUV并使用SDL进行播放
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="sdlVideo"></param>
        /// <returns></returns>
        public unsafe int RunVideo(string fileName, SDLHelper sdlVideo)
        {
            IsRun = true;
            exit_thread = false;
            pause_thread = false;
            threadVideo = Thread.CurrentThread;
            int error, frame_count = 0;
            int got_picture, ret;
            SwsContext* pSwsCtx = null;
            AVFormatContext* ofmt_ctx = null;
            IntPtr convertedFrameBufferPtr = IntPtr.Zero;
            try
            {
                // 注册编解码器
                ffmpeg.avcodec_register_all();

                // 获取文件信息上下文初始化
                ofmt_ctx = ffmpeg.avformat_alloc_context();

                // 打开媒体文件
                error = ffmpeg.avformat_open_input(&ofmt_ctx, fileName, null, null);
                if (error != 0)
                {
                    throw new ApplicationException($"ffmpeg avformat_open_input error {error}");
                }

                // 获取流的通道
                for (int i = 0; i < ofmt_ctx->nb_streams; i++)
                {
                    if (ofmt_ctx->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                    {
                        videoindex = i;
                        Console.WriteLine("video.............." + videoindex);
                    }
                }

                if (videoindex == -1)
                {
                    Console.WriteLine("Couldn't find a video stream.（没有找到视频流）");
                    return -1;
                }

                // 视频流处理
                if (videoindex > -1)
                {
                    //获取视频流中的编解码上下文
                    AVCodecContext* pCodecCtx = ofmt_ctx->streams[videoindex]->codec;

                    //根据编解码上下文中的编码id查找对应的解码
                    AVCodec* pCodec = ffmpeg.avcodec_find_decoder(pCodecCtx->codec_id);
                    if (pCodec == null)
                    {
                        Console.WriteLine("没有找到编码器");
                        return -1;
                    }

                    //打开编码器
                    if (ffmpeg.avcodec_open2(pCodecCtx, pCodec, null) < 0)
                    {
                        Console.WriteLine("编码器无法打开");
                        return -1;
                    }
                    Console.WriteLine("Find a  video stream.channel=" + videoindex);

                    //输出视频信息
                    var format = ofmt_ctx->iformat->name->ToString();
                    var len = (ofmt_ctx->duration) / 1000000;
                    var width = pCodecCtx->width;
                    var height = pCodecCtx->height;
                    Console.WriteLine("video format：" + format);
                    Console.WriteLine("video length：" + len);
                    Console.WriteLine("video width&height：width=" + width + " height=" + height);
                    Console.WriteLine("video codec name：" + pCodec->name->ToString());

                    //准备读取
                    //AVPacket用于存储一帧一帧的压缩数据（H264）
                    //缓冲区，开辟空间
                    AVPacket* packet = (AVPacket*)ffmpeg.av_malloc((ulong)sizeof(AVPacket));

                    //AVFrame用于存储解码后的像素数据(YUV)
                    //内存分配
                    AVFrame* pFrame = ffmpeg.av_frame_alloc();
                    //YUV420
                    AVFrame* pFrameYUV = ffmpeg.av_frame_alloc();
                    //只有指定了AVFrame的像素格式、画面大小才能真正分配内存
                    //缓冲区分配内存
                    int out_buffer_size = ffmpeg.avpicture_get_size(AVPixelFormat.AV_PIX_FMT_YUV420P, pCodecCtx->width, pCodecCtx->height);
                    byte* out_buffer = (byte*)ffmpeg.av_malloc((ulong)out_buffer_size);
                    //初始化缓冲区
                    ffmpeg.avpicture_fill((AVPicture*)pFrameYUV, out_buffer, AVPixelFormat.AV_PIX_FMT_YUV420P, pCodecCtx->width, pCodecCtx->height);

                    //用于转码（缩放）的参数，转之前的宽高，转之后的宽高，格式等
                    SwsContext* sws_ctx = ffmpeg.sws_getContext(pCodecCtx->width, pCodecCtx->height, AVPixelFormat.AV_PIX_FMT_YUV420P /*pCodecCtx->pix_fmt*/, pCodecCtx->width, pCodecCtx->height, AVPixelFormat.AV_PIX_FMT_YUV420P, ffmpeg.SWS_BICUBIC, null, null, null);

                    // 初始化sdl
                    sdlVideo.SDL_Init(pCodecCtx->width, pCodecCtx->height);

                    while (ffmpeg.av_read_frame(ofmt_ctx, packet) >= 0)
                    {
                        // 退出线程
                        if (exit_thread)
                        {
                            break;
                        }
                        // 暂停解析
                        if (pause_thread)
                        {
                            while (pause_thread)
                            {
                                Thread.Sleep(100);
                            }
                        }
                        //只要视频压缩数据（根据流的索引位置判断）
                        if (packet->stream_index == videoindex)
                        {
                            //解码一帧视频压缩数据，得到视频像素数据
                            ret = ffmpeg.avcodec_decode_video2(pCodecCtx, pFrame, &got_picture, packet);
                            if (ret < 0)
                            {
                                Console.WriteLine("视频解码错误");
                                return -1;
                            }

                            // 读取解码后的帧数据
                            if (got_picture > 0)
                            {
                                frame_count++;
                                Console.WriteLine("视频帧数:第 " + frame_count + " 帧");

                                //AVFrame转为像素格式YUV420，宽高
                                ffmpeg.sws_scale(sws_ctx, pFrame->data, pFrame->linesize, 0, pCodecCtx->height, pFrameYUV->data, pFrameYUV->linesize);

                                //SDL播放YUV数据
                                var data = out_buffer;
                                sdlVideo.SDL_Display(pCodecCtx->width, pCodecCtx->height, (IntPtr)data, out_buffer_size, pFrameYUV->linesize[0]);

                            }
                        }

                        //释放资源
                        ffmpeg.av_free_packet(packet);
                    }

                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                if (&ofmt_ctx != null)
                {
                    ffmpeg.avformat_close_input(&ofmt_ctx);//关闭流文件 
                }

            }
            IsRun = false;
            return 0;
        }

        /// <summary>
        /// 音频AAC转PCM并使用SDL进行播放
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="sdlAudio"></param>
        /// <returns></returns>
        public unsafe int RunAudio(string fileName, SDLAudio sdlAudio)
        {
            IsRun = true;
            exit_thread = false;
            pause_thread = false;
            threadAudio = Thread.CurrentThread;
            int error, frame_count = 0;
            int got_frame, ret;
            AVFormatContext* ofmt_ctx = null;
            SwsContext* pSwsCtx = null;
            IntPtr convertedFrameBufferPtr = IntPtr.Zero;
            try
            {
                // 注册编解码器
                ffmpeg.avcodec_register_all();

                // 获取文件信息上下文初始化
                ofmt_ctx = ffmpeg.avformat_alloc_context();

                // 打开媒体文件
                error = ffmpeg.avformat_open_input(&ofmt_ctx, fileName, null, null);
                if (error != 0)
                {
                    throw new ApplicationException($"ffmepg avformat_open_input error: {error}");
                }

                // 获取流的通道
                for (int i = 0; i < ofmt_ctx->nb_streams; i++)
                {
                    if (ofmt_ctx->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                    {
                        audioindex = i;
                        Console.WriteLine("audio.............." + audioindex);
                    }
                }

                if (audioindex == -1)
                {
                    Console.WriteLine("Couldn't find a  audio stream.（没有找到音频流）");
                    return -1;
                }

                // 音频流处理
                if (audioindex > -1)
                {
                    //根据索引拿到对应的流,根据流拿到解码器上下文
                    AVCodecContext* pCodeCtx = ofmt_ctx->streams[audioindex]->codec;

                    //再根据上下文拿到编解码id，通过该id拿到解码器
                    AVCodec* pCodec = ffmpeg.avcodec_find_decoder(pCodeCtx->codec_id);
                    if (pCodec == null)
                    {
                        Console.WriteLine("没有找到编码器");
                        return -1;
                    }
                    //打开编码器
                    if (ffmpeg.avcodec_open2(pCodeCtx, pCodec, null) < 0)
                    {
                        Console.WriteLine("编码器无法打开");
                        return -1;
                    }
                    Console.WriteLine("Find a  audio stream. channel=" + audioindex);

                    //编码数据
                    AVPacket* packet = (AVPacket*)ffmpeg.av_malloc((ulong)(sizeof(AVPacket)));
                    //解压缩数据
                    AVFrame* frame = ffmpeg.av_frame_alloc();

                    //frame->16bit 44100 PCM 统一音频采样格式与采样率
                    SwrContext* swrCtx = ffmpeg.swr_alloc();
                    //重采样设置选项-----------------------------------------------------------start
                    //输入的采样格式
                    AVSampleFormat in_sample_fmt = pCodeCtx->sample_fmt;
                    //输出的采样格式 16bit PCM
                    AVSampleFormat out_sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_S16;
                    //输入的采样率
                    int in_sample_rate = pCodeCtx->sample_rate;
                    //输出的采样率
                    int out_sample_rate = 44100;
                    //输入的声道布局
                    long in_ch_layout = (long)pCodeCtx->channel_layout;
                    //输出的声道布局
                    int out_ch_layout = ffmpeg.AV_CH_LAYOUT_MONO;

                    ffmpeg.swr_alloc_set_opts(swrCtx, out_ch_layout, out_sample_fmt, out_sample_rate, in_ch_layout, in_sample_fmt, in_sample_rate, 0, null);
                    ffmpeg.swr_init(swrCtx);
                    //重采样设置选项-----------------------------------------------------------end
                    //获取输出的声道个数
                    int out_channel_nb = ffmpeg.av_get_channel_layout_nb_channels((ulong)out_ch_layout);
                    //存储pcm数据
                    byte* out_buffer = (byte*)ffmpeg.av_malloc(2 * 44100);

                    //一帧一帧读取压缩的音频数据AVPacket
                    while (ffmpeg.av_read_frame(ofmt_ctx, packet) >= 0)
                    {
                        // 退出线程
                        if (exit_thread)
                        {
                            break;
                        }
                        // 暂停解析
                        if (pause_thread)
                        {
                            while (pause_thread)
                            {
                                Thread.Sleep(100);
                            }
                        }
                        if (packet->stream_index == audioindex)
                        {
                            //解码AVPacket->AVFrame
                            ret = ffmpeg.avcodec_decode_audio4(pCodeCtx, frame, &got_frame, packet);
                            if (ret < 0)
                            {
                                Console.WriteLine("音频解码失败");
                                return -1;
                            }
                            // 读取帧数据
                            if (got_frame > 0)
                            {
                                frame_count++;
                                Console.WriteLine("音频帧数:第 " + frame_count + " 帧");
                                var data_ = frame->data;
                                ffmpeg.swr_convert(swrCtx, &out_buffer, 2 * 44100, (byte**)&data_, frame->nb_samples);
                                //获取sample的size
                                int out_buffer_size = ffmpeg.av_samples_get_buffer_size(null, out_channel_nb, frame->nb_samples, out_sample_fmt, 1);
                                //写入文件进行测试
                                var data = out_buffer;
                                sdlAudio.PlayAudio((IntPtr)data, out_buffer_size);
                            }
                        }
                        ffmpeg.av_free_packet(packet);
                    }

                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                if (&ofmt_ctx != null)
                {
                    ffmpeg.avformat_close_input(&ofmt_ctx);//关闭流文件 
                }

            }
            IsRun = false;
            return 0;
        }


        /// <summary>
        /// 开启线程
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="sdlVideo"></param>
        public void Start(string fileName, SDLHelper sdlvideo, SDLAudio sdlAudio)
        {
            // 视频线程
            threadVideo = new Thread(() =>
            {
                try
                {
                    RunVideo(fileName, sdlvideo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("JT1078CodecForMp4.Run Video", ex);
                }
            });
            threadVideo.IsBackground = true;
            threadVideo.Start();

            // 音频线程
            //threadAudio = new Thread(() =>
            //{
            //    try
            //    {
            //        RunAudio(fileName, sdlAudio);
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine("JT1078CodecForMp4.Run Audio", ex);
            //    }
            //});
            //threadAudio.IsBackground = true;
            //threadAudio.Start();
        }

        /// <summary>
        /// 暂停继续
        /// </summary>
        public void GoOn()
        {
            pause_thread = false;

        }

        /// <summary>
        /// 暂停
        /// </summary>
        public void Pause()
        {
            pause_thread = true;
        }

        /// <summary>
        /// 停止
        /// </summary>
        public void Stop()
        {
            exit_thread = true;
        }
    }
}
