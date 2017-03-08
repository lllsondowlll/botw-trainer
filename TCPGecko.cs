#define DIRECT

namespace BotwTrainer
{
    using System;
    using System.IO;

    public class ByteSwap
    {
        public static UInt16 Swap(UInt16 input)
        {
            if (BitConverter.IsLittleEndian)
                return ((UInt16)(
                    ((0xFF00 & input) >> 8) |
                    ((0x00FF & input) << 8)));
            else
                return input;
        }

        public static UInt32 Swap(UInt32 input)
        {
            if (BitConverter.IsLittleEndian)
                return ((UInt32)(
                    ((0xFF000000 & input) >> 24) |
                    ((0x00FF0000 & input) >> 8) |
                    ((0x0000FF00 & input) << 8) |
                    ((0x000000FF & input) << 24)));
            else
                return input;
        }

        public static UInt64 Swap(UInt64 input)
        {
            if (BitConverter.IsLittleEndian)
                return ((UInt64)(
                    ((0xFF00000000000000 & input) >> 56) |
                    ((0x00FF000000000000 & input) >> 40) |
                    ((0x0000FF0000000000 & input) >> 24) |
                    ((0x000000FF00000000 & input) >> 8) |
                    ((0x00000000FF000000 & input) << 8) |
                    ((0x0000000000FF0000 & input) << 24) |
                    ((0x000000000000FF00 & input) << 40) |
                    ((0x00000000000000FF & input) << 56)));
            else
                return input;
        }
    }

    public enum ETCPErrorCode
    {
        noTCPGeckoFound,
        FTDICommandSendError,
        FTDIReadDataError,
        TooManyRetries
    }

    public enum FTDICommand
    {
        CMD_ResultError,
        CMD_FatalError,
        CMD_OK
    }

    public delegate void GeckoProgress(UInt32 address, UInt32 currentchunk, UInt32 allchunks, UInt32 transferred, UInt32 length, bool okay, bool dump);

    public class ETCPGeckoException : Exception
    {
        private ETCPErrorCode PErrorCode;

        public ETCPGeckoException(ETCPErrorCode code)
        {
            this.PErrorCode = code;
        }
    }

    public class TCPGecko
    {
        private TcpConn ptcp;

        #region base constants
        private const UInt32 packetsize = 0x400;
        private const Byte cmd_poke08 = 0x01;
        private const Byte cmd_poke16 = 0x02;
        private const Byte cmd_pokemem = 0x03;
        private const Byte cmd_readmem = 0x04;
        private const Byte cmd_writekern = 0x0b;
        private const Byte cmd_readkern = 0x0c;
        private const Byte cmd_os_version = 0x9A;

        private const Byte GCFAIL = 0xCC;

        private const Byte BlockZero = 0xB0;
        #endregion

        private event GeckoProgress PChunkUpdate;

        private bool Connected { get; set; }

        private bool CancelDump { get; set; }

        public TCPGecko(string host, int port)
        {
            this.ptcp = new TcpConn(host, port);
            this.Connected = false;
            this.PChunkUpdate = null;
        }

        ~TCPGecko()
        {
            if (this.Connected)
                this.Disconnect();
        }

        private bool InitGecko()
        {
            //Reset device
            return true;
        }

        public bool Connect()
        {
            if (this.Connected)
            {
                this.Disconnect();
            }

            this.Connected = false;

            //Open TCP Gecko
            try
            {
                this.ptcp.Connect();
            }
            catch (IOException)
            {
                // Don't disconnect if there's nothing connected
                this.Disconnect();
                throw new ETCPGeckoException(ETCPErrorCode.noTCPGeckoFound);
            }

            System.Threading.Thread.Sleep(150);
            this.Connected = true;

            return true;
        }

        public bool Disconnect()
        {
            this.Connected = false;
            this.ptcp.Close();

            return false;
        }

        protected FTDICommand GeckoRead(Byte[] recbyte, UInt32 nobytes)
        {
            UInt32 bytes_read = 0;

            try
            {
                this.ptcp.Read(recbyte, nobytes, ref bytes_read);
            }
            catch (IOException)
            {
                this.Disconnect();
                return FTDICommand.CMD_FatalError;       // fatal error
            }
            if (bytes_read != nobytes)
            {
                return FTDICommand.CMD_ResultError;   // lost bytes in transmission
            }

            return FTDICommand.CMD_OK;
        }

        protected FTDICommand GeckoWrite(Byte[] sendbyte, Int32 nobytes)
        {
            UInt32 bytes_written = 0;

            try
            {
                this.ptcp.Write(sendbyte, nobytes, ref bytes_written);
            }
            catch (IOException)
            {
                this.Disconnect();
                return FTDICommand.CMD_FatalError;       // fatal error
            }
            if (bytes_written != nobytes)
            {
                return FTDICommand.CMD_ResultError;   // lost bytes in transmission
            }

            return FTDICommand.CMD_OK;
        }

        //Send update on a running process to the parent class
        protected void SendUpdate(UInt32 address, UInt32 currentchunk, UInt32 allchunks, UInt32 transferred, UInt32 length, bool okay, bool dump)
        {
            if (this.PChunkUpdate != null)
                this.PChunkUpdate(address, currentchunk, allchunks, transferred, length, okay, dump);
        }

        public void Dump(UInt32 startdump, UInt32 enddump, Stream saveStream)
        {
            Stream[] tempStream = { saveStream };
            this.Dump(startdump, enddump, tempStream);
        }

        public void Dump(UInt32 startdump, UInt32 enddump, Stream[] saveStream)
        {
            //Reset connection
            this.InitGecko();

            if (ValidMemory.RangeCheckId(startdump) != ValidMemory.RangeCheckId(enddump))
            {
                enddump = ValidMemory.ValidAreas[ValidMemory.RangeCheckId(startdump)].High;
            }

            if (!ValidMemory.ValidAddress(startdump)) return;

            //How many bytes of data have to be transferred
            UInt32 memlength = enddump - startdump;

            //How many chunks do I need to split this data into
            //How big ist the last chunk
            UInt32 fullchunks = memlength / packetsize;
            UInt32 lastchunk = memlength % packetsize;

            //How many chunks do I need to transfer
            UInt32 allchunks = fullchunks;
            if (lastchunk > 0)
                allchunks++;

            UInt64 GeckoMemRange = ByteSwap.Swap((UInt64)(((UInt64)startdump << 32) + ((UInt64)enddump)));
            if (this.GeckoWrite(BitConverter.GetBytes(cmd_readmem), 1) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            //Read reply - expcecting GCACK -- nope, too slow, TCP is reliable!
            Byte retry = 0;

            //Now let's send the dump information
            if (this.GeckoWrite(BitConverter.GetBytes(GeckoMemRange), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            //We start with chunk 0
            UInt32 chunk = 0;
            retry = 0;

            // Reset cancel flag
            bool done = false;
            this.CancelDump = false;

            Byte[] buffer = new Byte[packetsize]; //read buffer
            while (chunk < fullchunks && !done)
            {
                //No output yet availible
                this.SendUpdate(startdump + chunk * packetsize, chunk, allchunks, chunk * packetsize, memlength, retry == 0, true);
                //Set buffer
                Byte[] response = new Byte[1];
                if (this.GeckoRead(response, 1) != FTDICommand.CMD_OK)
                {
                    //Major fail, give it up
                    this.GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }
                Byte reply = response[0];
                if (reply == BlockZero)
                {
                    for (int i = 0; i < packetsize; i++)
                    {
                        buffer[i] = 0;
                    }
                }
                else
                {
                    FTDICommand returnvalue = this.GeckoRead(buffer, packetsize);
                    if (returnvalue == FTDICommand.CMD_ResultError)
                    {
                        retry++;
                        if (retry >= 3)
                        {
                            //Give up, too many retries
                            this.GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                            throw new ETCPGeckoException(ETCPErrorCode.TooManyRetries);
                        }
                        //GeckoWrite(BitConverter.GetBytes(GCRETRY), 1);
                        continue;
                    }
                    else if (returnvalue == FTDICommand.CMD_FatalError)
                    {
                        //Major fail, give it up
                        this.GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                        throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                    }
                }
                //write received package to output stream
                foreach (Stream stream in saveStream)
                {
                    stream.Write(buffer, 0, ((Int32)packetsize));
                }

                //reset retry counter
                retry = 0;
                //next chunk
                chunk++;

                if (!this.CancelDump)
                {
                    //ackowledge package -- nope, too slow, TCP is reliable!
                    //GeckoWrite(BitConverter.GetBytes(GCACK), 1);
                }
                else
                {
                    // User requested a cancel
                    this.GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    done = true;
                }
            }

            //Final package?
            while (!done && lastchunk > 0)
            {
                //No output yet availible
                this.SendUpdate(startdump + chunk * packetsize, chunk, allchunks, chunk * packetsize, memlength, retry == 0, true);
                //Set buffer
                // buffer = new Byte[lastchunk];
                Byte[] response = new Byte[1];
                if (this.GeckoRead(response, 1) != FTDICommand.CMD_OK)
                {
                    //Major fail, give it up
                    this.GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                    throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                }
                Byte reply = response[0];
                if (reply == BlockZero)
                {
                    for (int i = 0; i < lastchunk; i++)
                    {
                        buffer[i] = 0;
                    }
                }
                else
                {
                    FTDICommand returnvalue = this.GeckoRead(buffer, lastchunk);
                    if (returnvalue == FTDICommand.CMD_ResultError)
                    {
                        retry++;
                        if (retry >= 3)
                        {
                            //Give up, too many retries
                            this.GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                            throw new ETCPGeckoException(ETCPErrorCode.TooManyRetries);
                        }
                        //GeckoWrite(BitConverter.GetBytes(GCRETRY), 1);
                        continue;
                    }
                    else if (returnvalue == FTDICommand.CMD_FatalError)
                    {
                        //Major fail, give it up
                        this.GeckoWrite(BitConverter.GetBytes(GCFAIL), 1);
                        throw new ETCPGeckoException(ETCPErrorCode.FTDIReadDataError);
                    }
                }
                //write received package to output stream
                foreach (Stream stream in saveStream)
                {
                    stream.Write(buffer, 0, ((Int32)lastchunk));
                }
                //reset retry counter
                retry = 0;
                //cancel while loop
                done = true;
                //ackowledge package -- nope, too slow, TCP is reliable!
                //GeckoWrite(BitConverter.GetBytes(GCACK), 1);
            }
            this.SendUpdate(enddump, allchunks, allchunks, memlength, memlength, true, true);
        }

        //Allows sending a basic one byte command to the Wii
        public FTDICommand RawCommand(Byte id)
        {
            return this.GeckoWrite(BitConverter.GetBytes(id), 1);
        }

        #region poke commands
        //Poke a 32 bit value - note: address and value must be all in endianness of sending platform
        public void poke(UInt32 address, UInt32 value)
        {
            //Lower address
            address &= 0xFFFFFFFC;

            //value = send [address in big endian] [value in big endian]
            UInt64 PokeVal = (((UInt64)address) << 32) | ((UInt64)value);

            PokeVal = ByteSwap.Swap(PokeVal);

            //Send poke
            if (this.RawCommand(cmd_pokemem) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            //write value
            if (this.GeckoWrite(BitConverter.GetBytes(PokeVal), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        //Copy of poke, just poke32 to make clear it is a 32-bit poke
        public void poke32(UInt32 address, UInt32 value)
        {
            this.poke(address, value);
        }

        //Poke a 16 bit value - note: address and value must be all in endianness of sending platform
        public void poke16(UInt32 address, UInt16 value)
        {
            //Lower address
            address &= 0xFFFFFFFE;

            //value = send [address in big endian] [value in big endian]
            UInt64 PokeVal = (((UInt64)address) << 32) | ((UInt64)value);

            PokeVal = ByteSwap.Swap(PokeVal);

            //Send poke16
            if (this.RawCommand(cmd_poke16) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            //write value
            if (this.GeckoWrite(BitConverter.GetBytes(PokeVal), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        //Poke a 08 bit value - note: address and value must be all in endianness of sending platform
        public void poke08(UInt32 address, Byte value)
        {
            //value = send [address in big endian] [value in big endian]
            UInt64 PokeVal = (((UInt64)address) << 32) | ((UInt64)value);

            PokeVal = ByteSwap.Swap(PokeVal);

            //Send poke08
            if (this.RawCommand(cmd_poke08) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            //write value
            if (this.GeckoWrite(BitConverter.GetBytes(PokeVal), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }
        #endregion

        #region kern commands
        //Poke a 32 bit value to kernel. note: address and value must be all in endianness of sending platform
        public void poke_kern(UInt32 address, UInt32 value)
        {
            //value = send [address in big endian] [value in big endian]
            UInt64 PokeVal = (((UInt64)address) << 32) | ((UInt64)value);

            PokeVal = ByteSwap.Swap(PokeVal);

            //Send poke
            if (this.RawCommand(cmd_writekern) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            //write value
            if (this.GeckoWrite(BitConverter.GetBytes(PokeVal), 8) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);
        }

        //Read a 32 bit value from kernel. note: address must be all in endianness of sending platform
        public UInt32 peek_kern(UInt32 address)
        {
            //value = send [address in big endian] [value in big endian]
            address = ByteSwap.Swap(address);

            //Send read
            if (this.RawCommand(cmd_readkern) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            //write value
            if (this.GeckoWrite(BitConverter.GetBytes(address), 4) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            Byte[] buffer = new Byte[4];
            if (this.GeckoRead(buffer, 4) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            return ByteSwap.Swap(BitConverter.ToUInt32(buffer, 0));
        }
        #endregion

        public UInt32 OsVersionRequest()
        {
            if (this.RawCommand(cmd_os_version) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            Byte[] buffer = new Byte[4];

            if (this.GeckoRead(buffer, 4) != FTDICommand.CMD_OK)
                throw new ETCPGeckoException(ETCPErrorCode.FTDICommandSendError);

            return ByteSwap.Swap(BitConverter.ToUInt32(buffer, 0));
        }

        public UInt32 peek(UInt32 address)
        {
            if (!ValidMemory.ValidAddress(address))
            {
                return 0;
            }

            //address will be alligned to 4
            UInt32 paddress = address & 0xFFFFFFFC;

            //Create a memory stream for the actual dump
            MemoryStream stream = new MemoryStream();

            //make sure to not send data to the output
            GeckoProgress oldUpdate = this.PChunkUpdate;
            this.PChunkUpdate = null;

            try
            {
                //dump data
                this.Dump(paddress, paddress + 4, stream);

                //go to beginning
                stream.Seek(0, SeekOrigin.Begin);
                Byte[] buffer = new Byte[4];
                stream.Read(buffer, 0, 4);

                //Read buffer
                UInt32 result = BitConverter.ToUInt32(buffer, 0);

                //Swap to machine endianness and return
                result = ByteSwap.Swap(result);

                return result;
            }
            finally
            {
                this.PChunkUpdate = oldUpdate;

                //make sure the Stream is properly closed
                stream.Close();
            }
        }
    }
}
