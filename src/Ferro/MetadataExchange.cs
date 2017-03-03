﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace Ferro
{
    public class MetadataExchange
    { 
        // extCode refers to the value peer has associated with ut_metadata
        public void RequestMetadata(NetworkStream stream, TcpClient connection, int extCode)
        {
            if (!connection.Connected)
            {
                throw new Exception("Disconnected from peer after handshake.");
            }

            var initialRequest = ConstructMessage(extCode, 0, 0);
            Console.WriteLine("Sending message: " + initialRequest.ToHuman());
            stream.Write(initialRequest);

            var theirPrefix = new byte[4];
            stream.Read(theirPrefix, 0, 4);
            var theirLength = theirPrefix.Decode32BitInteger();

            if (theirLength == 0)
            {
                throw new Exception("No response from peer; aborting.");
            }

            Console.WriteLine("Their length: " + theirLength);
            var peerResponse = new byte[theirLength];
            stream.Read(peerResponse, 0, theirLength);
            if (peerResponse[0] != 20)
            {
                Console.WriteLine("Unexpected payload; aborting.");
            }
        }


        // For messages with msgType 0 (request) and 2 (reject)
        public static byte[] ConstructMessage(int extCode, int msgType, int piece)
        {
            var messageDict = new Dictionary<byte[], object>();
            messageDict["msg_type".ToASCII()] = (Int64)msgType;
            messageDict["piece".ToASCII()] = (Int64)piece;
            var encodedMsg = Bencoding.Encode(messageDict);

            var length = BitConverter.GetBytes(encodedMsg.Length + 2);
            Array.Reverse(length);

            var message = new byte[encodedMsg.Length + 6];
            length.CopyTo(message, 0);
            message[4] = 20;
            message[5] = (byte)extCode;
            encodedMsg.CopyTo(message, 6);

            return message;
        }

        // For messages with msgType 1 (data)
        public static byte[] ConstructMessage(int extCode, int msgType, int piece, int length)
        {
            return new byte[4];
        }
    }
}
