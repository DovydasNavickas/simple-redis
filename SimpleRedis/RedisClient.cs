using System;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SimpleRedis
{

    public class RedisException : Exception
    {
        public RedisException(string message) : base(message) { }
    }
    public sealed class RedisClient : DynamicObject, IDisposable
    {
        private NetworkStream netStream;
        private BufferedStream outStream;
        private MemoryStream buffer;
        public RedisClient(string host = "127.0.0.1", int port = 6379)
        {
            Socket socket = null;
            try {
                // connect
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.NoDelay = true;
                socket.Connect(new DnsEndPoint(host, port));
                netStream = new NetworkStream(socket, true);
                // now owned by netstream, so can avoid wiping
                socket = null;
                // buffer when writing to avoid excessive packet fragmentation
                outStream = new BufferedStream(netStream, 2048);
                buffer = new MemoryStream();
            }
            catch
            {
                if (socket != null) socket.Dispose();
                Dispose();
                throw;
            }
        }
        static void Dispose<T>(ref T field) where T : class, IDisposable
        {
            if (field != null) { try { field.Dispose(); } catch { } finally { field = null; } }
        }
        public void Dispose()
        {
            Dispose(ref outStream);
            Dispose(ref netStream);
            Dispose(ref buffer);
        }
        
        private void WriteRaw(Stream target, char value)
        {
            if (value < 128)
            {
                outStream.WriteByte((byte)value);
            }
            else
            {
                WriteRaw(target, value.ToString(CultureInfo.InvariantCulture));
            }
            
        }
        private static void WriteRaw(Stream target, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            target.Write(bytes, 0, bytes.Length);
        }
        private static void WriteRaw(Stream target, int value)
        {
            if (value >= 0 && value < 10)
            {
                target.WriteByte((byte)('0' + value));
            }
            else
            {
                WriteRaw(target, value.ToString(CultureInfo.InvariantCulture));
            }
        }
        private static void WriteRaw(Stream target, byte[] value)
        {
            target.Write(value, 0, value.Length);
        }
        private static void WriteRaw(Stream target, ArraySegment<byte> value)
        {
            target.Write(value.Array, value.Offset, value.Count);
        }
        private void WriteEndLine()
        {
            WriteRaw(outStream, '\r');
            WriteRaw(outStream, '\n');
        }
        private void WriteArg(object value)
        {
            // need to know the length, so: write to our memory-stream
            // first
            buffer.SetLength(0);
            WriteRaw(buffer, (dynamic)value);
            // now write that to the (bufferred) output
            WriteRaw(outStream, '$');
            WriteRaw(outStream, (int)buffer.Length);
            WriteEndLine();
            WriteRaw(outStream, new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length));
            WriteEndLine();
        }
        private void WriteCommand(string name, object[] args)
        {
            WriteRaw(outStream, '*');
            WriteRaw(outStream, 1 + args.Length);
            WriteEndLine();
            WriteArg(name);
            for (int i = 0; i < args.Length; i++)
            {
                WriteArg(args[i]);
            }
            // and make sure we aren't holding onto any data...
            outStream.Flush(); // flushes to netStream
            netStream.Flush(); // just to be sure! (although this is a no-op, IIRC)
        }
        private static Exception EOF()
        {
            throw new EndOfStreamException("The server has disconnected");
        }
        byte[] ReadToNewline()
        {
            var ms = new MemoryStream();
            int val;
            do
            {
                val = netStream.ReadByte();
                if (val < 0) throw EOF();
                if (val == '\r')
                {
                    val = netStream.ReadByte();
                    if (val == '\n') return ms.ToArray();
                    throw new InvalidOperationException("Expected end-of-line");
                }
                ms.WriteByte((byte)val);
            } while (true);
        }
        int ReadLength()
        {
            var lenBlob = ReadToNewline();
            if(lenBlob.Length == 1)
            {
                int len = lenBlob[0] - '0';
                if (len < 0 || len > 9) throw new InvalidOperationException("Error reading bulk-reply");
                return len;
            }
            return int.Parse(Encoding.ASCII.GetString(lenBlob), CultureInfo.InvariantCulture);
        }
        RedisResult ReadBulk()
        {
            int len = ReadLength(), offset = 0, read;
            if (len == -1) return new RedisResult(null);
            byte[] data = new byte[len];
            while (len > 0 && (read = netStream.Read(data, offset, len)) > 0)
            {
                len -= read;
                offset += read;
            }
            if (len != 0) throw EOF();
            ReadEndOfLine();
            return new RedisResult(data);
        }
        void ReadEndOfLine()
        {
            if(netStream.ReadByte() != '\r' || netStream.ReadByte() != '\n')
                throw new InvalidOperationException("Expected end-of-line");
        }
        object[] ReadMultiBulk()
        {
            int len = ReadLength();
            if (len == -1) return null;
            object[] results = new object[len];
            for (int i = 0; i < len; i++)
            {
                results[i] = ReadResult();
            }
            return results;
        }
        private object ReadResult()
        {
            var type = netStream.ReadByte();
            if (type < 0) throw EOF();

            switch(type)
            {
                case (byte)'+': // status
                    return new RedisStatusResult(ReadToNewline());
                case (byte)'-': // error
                    return new RedisExceptionResult(ReadToNewline());
                case (byte)':': // integer
                    return new RedisResult(ReadToNewline());
                case (byte)'$': // bulk
                    return ReadBulk();
                case (byte)'*': // multi-bulk
                    return ReadMultiBulk();
                default:
                    throw new NotSupportedException("Unexpected reply type: " + (char)type);
            }
        }
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            WriteCommand(binder.Name, args);
            result = ReadResult();
            var err = result as RedisExceptionResult;
            if (err != null) throw err.GetException();
            return true;
        }


    }
}