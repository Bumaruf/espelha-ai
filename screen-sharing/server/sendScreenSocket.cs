﻿using server.Properties;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace server
{
    public class sendScreenSocket
    {
        Thread senderThread;
        IPEndPoint endpoint;
        Socket server;

        public sendScreenSocket(IPEndPoint Endpoint)
        {
            endpoint = Endpoint;
            server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            senderThread = new Thread(sender);
        }

        public void startSender()
        {
            senderThread.Start();
        }

        public void stopSender()
        {
            senderThread.Abort();
        }

        public void sender()
        {
            while (true)
            {
                sendSockets();
            }
        }

        // Print Screen
        private static Bitmap GrabDesktop()
        {
            Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
            Bitmap screenshot = new Bitmap(screenBounds.Width, screenBounds.Height, PixelFormat.Format24bppRgb);

            using (Graphics gfxScreenshot = Graphics.FromImage(screenshot))
            {
                gfxScreenshot.CopyFromScreen(screenBounds.X, screenBounds.Y, 0, 0, screenBounds.Size, CopyPixelOperation.SourceCopy);

                // Apply cursor
                Icon icon = Resources.cursor;
                gfxScreenshot.DrawIcon(icon, Cursor.Position.X, Cursor.Position.Y);
            }

            return screenshot;
        }

        // Preenche espaços vazios com caracteres
        private byte[] fillingSpaces(byte[] byteImg)
        {
            byte[] imageSize;
            switch (Encoding.ASCII.GetBytes(byteImg.Length.ToString()).Length)
            {
                case 4:
                    imageSize = Encoding.ASCII.GetBytes(byteImg.Length.ToString() + "---");
                    break;
                case 5:
                    imageSize = Encoding.ASCII.GetBytes(byteImg.Length.ToString() + "--");
                    break;
                case 6:
                    imageSize = Encoding.ASCII.GetBytes(byteImg.Length.ToString() + "-");
                    break;
                default:
                    imageSize = Encoding.ASCII.GetBytes(byteImg.Length.ToString());
                    break;
            }
            return imageSize;
        }

        // Realiza as divisões e envia-as
        private void sendSockets()
        {
            MemoryStream ms = new MemoryStream();
            Bitmap bmp = new Bitmap(GrabDesktop());
            bmp.Save(ms, ImageFormat.Jpeg);

            // Server connect
            server.Connect(endpoint);

            try
            {
                Byte[] byteImg = ms.ToArray(); // Image to Byte array

                int parts = byteImg.Length / 64999; // Dividindo tamanho da imagem em partes de 64999

                byte[] imageSize = fillingSpaces(byteImg);
                if (parts < 1)
                {
                    // Initial Socket
                    Byte[] bytesStart = new Byte[65008];
                    bytesStart[0] = Convert.ToByte(0); // Informa a númeração do bloco
                    bytesStart[8] = Convert.ToByte(0); // Adiciona no 0 a quantidade de blocos a serem enviados
                    Buffer.BlockCopy(imageSize, 0, bytesStart, 1, 7); // Adiciona no 1, 2, 3, 4, 5, 6, 7 o tamanho total da imagem
                    Buffer.BlockCopy(byteImg, 0, bytesStart, 9, byteImg.Length - (64999 * parts)); // Copia bloco da imagem pra dentro do array
                    server.Send(bytesStart); // Send

                    //Console.WriteLine("Frame"); // LOG
                }
                else
                {
                    // Initial Socket
                    Byte[] bytesStart = new Byte[65008];
                    bytesStart[0] = Convert.ToByte(0); // Informa a númeração do bloco
                    bytesStart[8] = Convert.ToByte(parts.ToString()); // Adiciona no 0 a quantidade de blocos a serem enviados
                    Buffer.BlockCopy(imageSize, 0, bytesStart, 1, 7); // Adiciona no 1, 2, 3, 4, 5, 6, 7 o tamanho total da imagem
                    Buffer.BlockCopy(byteImg, 0, bytesStart, 9, 64999); // Copia bloco da imagem pra dentro do array
                    server.Send(bytesStart); // Send packet

                    //Console.WriteLine("0 - Inicio"); // LOG

                    // Send parts
                    for (int i = 1; i < parts; i++)
                    {
                        Byte[] bytes = new Byte[65000];
                        bytes[0] = Convert.ToByte(i); // Informa a númeração do bloco
                        Buffer.BlockCopy(byteImg, 64999 * i, bytes, 1, 64999); // Copia bloco da imagem pra dentro do array
                        server.Send(bytes); // Send packet

                        //Console.WriteLine(i + " - Parte"); // LOG
                    }

                    // Final Socket
                    Byte[] bytesFinal = new Byte[65000];
                    bytesFinal[0] = Convert.ToByte(parts); // Informa que é o bloco final
                    Buffer.BlockCopy(byteImg, 64999 * parts, bytesFinal, 1, byteImg.Length - (64999 * parts));
                    server.Send(bytesFinal); // Send packet

                    //Console.WriteLine(parts + " - Final"); // LOG
                }
            }
            catch (Exception err)
            {
                Console.WriteLine("Exception: " + err.Message);
            }
        }
    }
}