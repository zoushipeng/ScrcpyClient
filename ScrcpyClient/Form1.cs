using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Net;
using System.Net.Sockets;
using System.IO;

namespace ScrcpyClient
{
    public partial class Form1 : Form
    {
        int port = 45450;
        FileStream fs;
        private SDLHelper sdlvideo;
        private SDLAudio sdlaudio = new SDLAudio();

        public Form1()
        {
            InitializeComponent();
            sdlvideo = new SDLHelper(panel1.Width, panel1.Height, panel1.Handle);
            sdlaudio.SDL_Init();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            Task.Factory.StartNew( () => {

                TcpClient client = new TcpClient(textBox1.Text.ToString(), port);
                NetworkStream stream = client.GetStream();

                var h264Socket = new H264SocketParser(sdlvideo);
                h264Socket.StartPlay(stream);

                //fs = new FileStream($"test{DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss")}.h264", FileMode.Create, FileAccess.Write);

                //int count = 0;
                //byte[] buffer = new byte[1024];
                //while((count = stream.Read(buffer, 0, 1024)) != 0)
                //{
                //    Console.WriteLine($"receive {count}");
                //    fs.Write(buffer, 0, count);
                //}
                Console.WriteLine("exit");
                this.BeginInvoke((Action)(() => {
                    button1.Enabled = true;
                }), null);
            });
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string fileName = "test.mp4";
            // 线程读取音视频流
            var jt1078CodecForMp4 = new JT1078CodecForMp4();
            jt1078CodecForMp4.Start(fileName, sdlvideo, sdlaudio);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            sdlvideo.UnInit();
        }

        private void button3_Click(object sender, EventArgs e)
        {

        }
    }
}
