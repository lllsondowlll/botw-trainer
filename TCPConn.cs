namespace BotwTrainer
{
    using System;
    using System.IO;
    using System.Net.Sockets;

    public class TcpConn
    {
        private TcpClient client;
        private NetworkStream stream;

        public TcpConn(string host, int port)
        {
            this.Host = host;
            this.Port = port;
            this.client = null;
            this.stream = null;
        }

        public string Host { get; private set; }

        public int Port { get; private set; }

        public void Connect()
        {
            try
            {
                this.Close();
            }
            catch (Exception ex)
            {
                //   
            }

            this.client = new TcpClient { NoDelay = true };
            var ar = this.client.BeginConnect(this.Host, this.Port, null, null);
            var wh = ar.AsyncWaitHandle;
            try
            {
                if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5), false))
                {
                    this.client.Close();
                    throw new IOException("Connection timeout.", new TimeoutException());
                }

                this.client.EndConnect(ar);
            }
            finally
            {
                wh.Close();
            }

            this.stream = this.client.GetStream();
            this.stream.ReadTimeout = 10000;
            this.stream.WriteTimeout = 10000;
        }

        public void Close()
        {
            try
            {
                if (this.client == null)
                {
                    throw new IOException("Not connected.", new NullReferenceException());
                }

                this.client.Close();
            }
            catch (Exception ex)
            {
                //
            }
            finally
            {
                this.client = null;
            }
        }

        public void Purge()
        {
            if (this.stream == null)
            {
                throw new IOException("Not connected.", new NullReferenceException());
            }

            this.stream.Flush();
        }

        public void Read(byte[] buffer, uint nobytes, ref uint bytesRead)
        {
            try
            {
                var offset = 0;
                if (this.stream == null)
                {
                    throw new IOException("Not connected.", new NullReferenceException());
                }

                bytesRead = 0;
                while (nobytes > 0)
                {
                    var read = this.stream.Read(buffer, offset, (int)nobytes);
                    if (read >= 0)
                    {
                        bytesRead += (uint)read;
                        offset += read;
                        nobytes -= (uint)read;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (ObjectDisposedException e)
            {
                throw new IOException("Connection closed.", e);
            }
        }

        public void Write(byte[] buffer, int nobytes, ref uint bytesWritten)
        {
            try
            {
                if (this.stream == null)
                {
                    throw new IOException("Not connected.", new NullReferenceException());
                }

                this.stream.Write(buffer, 0, nobytes);
                if (nobytes >= 0)
                {
                    bytesWritten = (uint)nobytes;
                }
                else
                {
                    bytesWritten = 0;
                }

                this.stream.Flush();
            }
            catch (ObjectDisposedException e)
            {
                throw new IOException("Connection closed.", e);
            }
        }
    }
}
