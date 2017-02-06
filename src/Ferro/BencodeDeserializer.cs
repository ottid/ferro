﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace Ferro
{
    public class BencodeDeserializer
    {
        // DECLARE STATE CONSTANTS
        private const int NeutralState = 0;
        private const int IntState = 1;
        private const int StringState = 2;
        private const int ListState = 3;
        private const int DictState = 4;

        // State field -- 
        // This keeps track of the type of thing we're 
        // deserializing at a given time
        private static Stack<int> state = new Stack<int>();

        // DECLARE DELIMITERS
        private static byte endDelimiter = "e".ToASCII()[0];
        private static byte intBeginDelimiter = "i".ToASCII()[0];
        private static byte listBeginDelimiter = "l".ToASCII()[0];

        private static Int64 IntDeserialize(byte[] bytes)
        {
            var output = new MemoryStream();
            foreach (var item in bytes)
            {
                if (item != endDelimiter)
                {
                    output.WriteByte(item);
                }
                else
                {
                    break;
                }
            }

            try
            {
                //state.Pop();
                return Int64.Parse(output.ToArray().FromASCII());
            }
            catch (OverflowException e)
            {
                throw new DeserializationException("Gigantic Integers are unsupported.", e);
            }
            catch (FormatException e)
            {
                // This is rather broad, catches multiple conditions.
                throw new DeserializationException("Incorrect format for Integer.", e);
            }

        }

        private static byte[] StringDeserialize(byte[] bytes)
        {
            var output = new MemoryStream();
            var numStore = new List<byte>();
            byte[] byteArray = bytes;
            Int32 length;

            foreach (var item in byteArray)
            {
                if (item == (byte)':')
                {
                    byteArray = byteArray.Skip(1).ToArray();
                    break;
                }
                else
                {
                    numStore.Add(item);
                    byteArray = byteArray.Skip(1).ToArray();
                }
            }

            try
            {
                length = Int32.Parse(numStore.ToArray().FromASCII());
            }
            catch (OverflowException e)
            {
                throw new DeserializationException("The String length value is too large", e);
            }

            if (length < 0)
            {
                throw new DeserializationException("Strings cannot have a negative length.");
            }

            state.Pop();
            return byteArray.Take(length).ToArray();
        }

        private static List<object> ListDeserialize(byte[] bytes)
        {
            var output = new List<object>();
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == endDelimiter && state.Peek() != ListState)
                {
                    state.Pop();
                    
                } else if (bytes[i] == endDelimiter)
                {
                    state.Pop();
                    return output;
                }
                else if (bytes[i] == intBeginDelimiter)
                {
                    output.Add(DeserializeAny(bytes.Skip(i).ToArray()));
                }
            }

            throw new DeserializationException("Lists must have an end delimiter.");
        }

        private static object DeserializeAny(byte[] bytes)
        {
            var output = new MemoryStream();
            foreach (var item in bytes)
            {
                if (item == listBeginDelimiter)
                {
                    state.Push(ListState);
                    return ListDeserialize(bytes.Skip(1).ToArray());
                }
                else if (item == intBeginDelimiter)
                {
                    state.Push(IntState);
                    return IntDeserialize(bytes.Skip(1).ToArray());
                }
                else if ((byte)'0' <= item && (byte)'9' >= item)
                {
                    state.Push(StringState);
                    return StringDeserialize(bytes);
                }
            }

            return output.ToArray().FromASCII();
        }

        public static object Deserialize(byte[] bytes)
        {
            return DeserializeAny(bytes);
        }
    }

    public class DeserializationException : Exception {
        public DeserializationException(string message) : 
            base(message) { }
        public DeserializationException(string message, Exception inner) : 
            base(message, inner) { }
    }
}
