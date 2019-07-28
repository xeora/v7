﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Xeora.Web.Service.Dss
{
    internal class ExternalDss : Basics.Dss.IDss, IDssService
    {
        private readonly RequestHandler _RequestHandler;
        private readonly ResponseHandler _ResponseHandler;

        public ExternalDss(ref RequestHandler requestHandler, ref ResponseHandler responseHandler, string uniqueId, DateTime expireDate)
        {
            this._RequestHandler = requestHandler;
            this._ResponseHandler = responseHandler;

            this.UniqueId = uniqueId;
            this.Expires = expireDate;
        }

        public object this[string key]
        {
            get
            {
                if (string.IsNullOrEmpty(key))
                    throw new ArgumentNullException(nameof(key));
                
                return this.Get(key);
            }
            set
            {
                if (string.IsNullOrEmpty(key))
                    throw new ArgumentNullException(nameof(key));

                if (key.Length > 128)
                    throw new OverflowException("key can not be longer than 128 characters");
                
                this.Set(key, value);
            }
        }

        public string UniqueId { get; private set; }
        public DateTime Expires { get; private set; }
        public string[] Keys => this.GetKeys();

        private object Get(string key)
        {
            byte[] responseBytes = null;

            long requestId;

            // Make Request
            BinaryWriter binaryWriter = null;
            Stream requestStream = null;

            try
            {
                requestStream = new MemoryStream();
                binaryWriter = new BinaryWriter(requestStream);

                /*
                 * -> GET\BYTE\CHARS{BYTEVALUELENGTH}\BYTE\CHARS{BYTEVALUELENGTH}
                 */

                binaryWriter.Write("GET".ToCharArray());
                binaryWriter.Write((byte)this.UniqueId.Length);
                binaryWriter.Write(this.UniqueId.ToCharArray());
                binaryWriter.Write((byte)key.Length);
                binaryWriter.Write(key.ToCharArray());
                binaryWriter.Flush();

                requestId = this._RequestHandler.MakeRequest(((MemoryStream)requestStream).ToArray());
            }
            finally
            {
                binaryWriter?.Close();
                requestStream?.Close();
            }

            responseBytes = this._ResponseHandler.WaitForMessage(requestId);
            if (responseBytes == null || responseBytes.Length == 0)
                return null;
            
            // Parse Response
            BinaryReader binaryReader = null;
            Stream responseStream = null;

            try
            {
                responseStream = new MemoryStream(responseBytes, 0, responseBytes.Length, false);
                binaryReader = new BinaryReader(responseStream);

                /*
                 * <- \BYTE\CHARS{BYTEVALUELENGTH}\INTEGER\BYTES{INTEGERVALUELENGTH}
                 */

                byte remoteKeyLength = binaryReader.ReadByte();
                string remoteKey = 
                    new string(binaryReader.ReadChars(remoteKeyLength));

                int remoteValueLength = binaryReader.ReadInt32();
                byte[] remoteValueBytes = 
                    binaryReader.ReadBytes(remoteValueLength);

                if (string.Compare(remoteKey, key) != 0)
                    return null;

                return this.DeSerialize(remoteValueBytes);
            }
            finally
            {
                binaryReader?.Close();
                responseStream?.Close();
            }
        }

        private void Set(string key, object value)
        {            
            // Make Request
            BinaryWriter binaryWriter = null;
            Stream requestStream = null;

            try
            {
                requestStream = new MemoryStream();
                binaryWriter = new BinaryWriter(requestStream);

                /*
                 * -> SET\BYTE\CHARS{BYTEVALUELENGTH}\BYTE\CHARS{BYTEVALUELENGTH}\INTEGER\BYTES{INTEGERVALUELENGTH}
                 */

                byte[] valueBytes = this.Serialize(value);
                if (valueBytes.Length > 16777000)
                    throw new OverflowException("Value is too big to store");

                binaryWriter.Write("SET".ToCharArray());
                binaryWriter.Write((byte)this.UniqueId.Length);
                binaryWriter.Write(this.UniqueId.ToCharArray());
                binaryWriter.Write((byte)key.Length);
                binaryWriter.Write(key.ToCharArray());
                binaryWriter.Write(valueBytes.Length);
                binaryWriter.Write(valueBytes, 0, valueBytes.Length);
                binaryWriter.Flush();

                long requestId =
                    this._RequestHandler.MakeRequest(((MemoryStream)requestStream).ToArray());

                byte[] responseBytes = this._ResponseHandler.WaitForMessage(requestId);
                if (responseBytes == null || responseBytes.Length == 0)
                    return;

                if (responseBytes[0] != 1)
                    throw new DssValueException();
            }
            finally
            {
                binaryWriter?.Close();
                requestStream?.Close();
            }
        }

        private string[] GetKeys()
        {
            List<string> keys = new List<string>();

            byte[] responseBytes = null;
            long requestId;

            // Make Request
            BinaryWriter binaryWriter = null;
            Stream requestStream = null;

            try
            {
                requestStream = new MemoryStream();
                binaryWriter = new BinaryWriter(requestStream);

                /*
                 * -> KYS\BYTE\CHARS{BYTEVALUELENGTH}
                 */

                binaryWriter.Write("KYS".ToCharArray());
                binaryWriter.Write((byte)this.UniqueId.Length);
                binaryWriter.Write(this.UniqueId.ToCharArray());
                binaryWriter.Flush();

                requestId = this._RequestHandler.MakeRequest(((MemoryStream)requestStream).ToArray());
            }
            catch
            {
                throw new ExternalCommunicationException();
            }
            finally
            {
                binaryWriter?.Close();
                requestStream?.Close();
            }

            responseBytes = this._ResponseHandler.WaitForMessage(requestId);
            if (responseBytes == null || responseBytes.Length == 0)
                return keys.ToArray();
            
            // Parse Response
            BinaryReader binaryReader = null;
            Stream responseStream = null;

            try
            {
                responseStream = new MemoryStream(responseBytes, 0, responseBytes.Length, false);
                binaryReader = new BinaryReader(responseStream);

                /*
                 * <- \BYTE\CHARS{BYTEVALUELENGTH}\BYTE\CHARS{BYTEVALUELENGTH}\BYTE\CHARS{BYTEVALUELENGTH}...
                 */

                do
                {
                    byte remoteKeyLength = binaryReader.ReadByte();
                    if (remoteKeyLength == 0)
                        break;
                    
                    keys.Add(new string(binaryReader.ReadChars(remoteKeyLength)));
                } while (binaryReader.PeekChar() > -1);
            }
            catch
            {
                throw new ExternalCommunicationException();
            }
            finally
            {
                binaryReader?.Close();
                responseStream?.Close();
            }

            return keys.ToArray();
        }

        private byte[] Serialize(object value)
        {
            Stream forStream = null;
            try
            {
                forStream = new MemoryStream();

                BinaryFormatter binFormater = new BinaryFormatter();
                binFormater.Serialize(forStream, value);

                return ((MemoryStream)forStream).ToArray();
            }
            catch (Exception)
            {
                return new byte[] { };
            }
            finally
            {
                forStream?.Close();
            }
        }

        private object DeSerialize(byte[] value)
        {
            Stream forStream = null;
            try
            {
                forStream = new MemoryStream(value);

                BinaryFormatter binFormater = new BinaryFormatter();

                return binFormater.Deserialize(forStream);
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                forStream?.Close();
            }
        }

        public bool IsExpired => throw new NotImplementedException();
        public void Extend() =>
            throw new NotImplementedException();
    }
}
