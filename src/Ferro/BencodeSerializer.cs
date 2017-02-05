﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ferro
{
    /// <summary>
    /// <para>Helper class, containing methods to serialize data into 
    /// Bencode format</para>
    /// </summary>
    // I'm going to want to change the name of this eventually
    public static class BencodeSerializer
    {
        public static byte[] Serialize(byte[] byteArray)
        {
            var output = new MemoryStream();
            output.Write(Encoding.ASCII.GetBytes(byteArray.Length.ToString()));
            output.Write(Encoding.ASCII.GetBytes(":"));
            output.Write(Encoding.ASCII.GetBytes(byteArray.ToString()));
            return output.ToArray();   
        }

        public static byte[] Serialize(int integer)
        {
           var output = new MemoryStream();
           output.Write(Encoding.ASCII.GetBytes("i"));
           output.Write(Encoding.ASCII.GetBytes(integer.ToString()));
           output.Write(Encoding.ASCII.GetBytes("e"));
           return output.ToArray();
        }

        public static string Serialize(List<object> list)
        {
            var output = "";
            list.ForEach(item => output += BencodeSerializer.Serialize(item));

            return "l" + output + "e";
        }

        public static string Serialize(Dictionary<object, object> dict)
        {
            return "test";
        }
    }
}
