﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Xeora.Web.Basics;
using Xeora.Web.Exceptions;

namespace Xeora.Web.Service.Dss.External
{
    internal class Service : Basics.Dss.IDss, IService
    {
        private readonly RequestHandler _RequestHandler;
        private readonly ResponseHandler _ResponseHandler;

        public Service(ref RequestHandler requestHandler, ref ResponseHandler responseHandler, string uniqueId, bool reusing, DateTime expireDate)
        {
            this._RequestHandler = requestHandler;
            this._ResponseHandler = responseHandler;

            this.UniqueId = uniqueId;
            this.Reusing = reusing;
            this.Expires = expireDate;
        }

        public string UniqueId { get; }
        public bool Reusing { get; }
        public DateTime Expires { get; }

        public object Get(string key, string lockCode = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
                
            return this._Get(key, lockCode);
        }
        
        public void Set(string key, object value, string lockCode = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            if (key.Length > 128)
                throw new OverflowException("key can not be longer than 128 characters");
                
            this._Set(key, value, lockCode);
        }

        public string Lock(string key) => this._Lock(key);
        public void Release(string key, string lockCode) => this._Release(key, lockCode);
        public string[] Keys => this.GetKeys();

        private object _Get(string key, string lockCode)
        {
            if (string.IsNullOrEmpty(lockCode)) 
                lockCode = string.Empty;
            
            long requestId;

            // Make Request
            BinaryWriter binaryWriter = null;
            Stream requestStream = null;

            try
            {
                requestStream = new MemoryStream();
                binaryWriter = new BinaryWriter(requestStream);

                /*
                 * -> GET\BYTE\CHARS{BYTEVALUELENGTH}\BYTE\CHARS{BYTEVALUELENGTH}\BYTE\CHARS{BYTEVALUELENGTH}
                 */

                binaryWriter.Write("GET".ToCharArray());
                binaryWriter.Write((byte)this.UniqueId.Length);
                binaryWriter.Write(this.UniqueId.ToCharArray());
                binaryWriter.Write((byte)key.Length);
                binaryWriter.Write(key.ToCharArray());
                binaryWriter.Write((byte)lockCode.Length);
                binaryWriter.Write(lockCode.ToCharArray());
                binaryWriter.Flush();

                requestId = 
                    this._RequestHandler.MakeRequest(((MemoryStream)requestStream).ToArray());
                if (requestId == -1) return null;
            }
            finally
            {
                binaryWriter?.Close();
                requestStream?.Close();
            }

            byte[] responseBytes = 
                this._ResponseHandler.WaitForMessage(requestId);
            if (responseBytes == null || responseBytes.Length == 0)
                return null;
            
            // Parse Response
            BinaryReader binaryReader = null;
            Stream responseStream = null;

            try
            {
                responseStream = 
                    new MemoryStream(responseBytes, 0, responseBytes.Length, false);
                binaryReader = new BinaryReader(responseStream);

                /*
                 * <- \BYTE\CHARS{BYTEVALUELENGTH}\BYTE\INTEGER\BYTES{INTEGERVALUELENGTH}
                 */

                byte remoteKeyLength = binaryReader.ReadByte();
                string remoteKey = 
                    new string(binaryReader.ReadChars(remoteKeyLength));

                byte remoteResult = 
                    binaryReader.ReadByte();

                if (remoteResult == 0)
                {
                    int remoteValueLength = binaryReader.ReadInt32();
                    byte[] remoteValueBytes =
                        binaryReader.ReadBytes(remoteValueLength);

                    return string.CompareOrdinal(remoteKey, key) == 0
                        ? this.DeSerialize(remoteValueBytes)
                        : null;
                }

                if (remoteResult == 1) // KeyLockedException
                {
                    if (string.CompareOrdinal(remoteKey, key) == 0)
                        throw new KeyLockedException();
                    return null;
                }
                
                if (string.CompareOrdinal(remoteKey, key) == 0)
                    throw new DssCommandException();
                return null;
            }
            finally
            {
                binaryReader?.Close();
                responseStream?.Close();
            }
        }

        private void _Set(string key, object value, string lockCode)
        {
            if (string.IsNullOrEmpty(lockCode))
                lockCode = string.Empty;
            
            // Make Request
            BinaryWriter binaryWriter = null;
            Stream requestStream = null;

            try
            {
                requestStream = new MemoryStream();
                binaryWriter = new BinaryWriter(requestStream);

                /*
                 * -> SET\BYTE\CHARS{BYTEVALUELENGTH}\BYTE\CHARS{BYTEVALUELENGTH}\BYTE\CHARS{BYTEVALUELENGTH}\INTEGER\BYTES{INTEGERVALUELENGTH}
                 */

                byte[] valueBytes = this.Serialize(value);
                if (valueBytes.Length > 16777000)
                    throw new OverflowException("Value is too big to store");

                binaryWriter.Write("SET".ToCharArray());
                binaryWriter.Write((byte)this.UniqueId.Length);
                binaryWriter.Write(this.UniqueId.ToCharArray());
                binaryWriter.Write((byte)key.Length);
                binaryWriter.Write(key.ToCharArray());
                binaryWriter.Write((byte)lockCode.Length);
                binaryWriter.Write(lockCode.ToCharArray());
                binaryWriter.Write(valueBytes.Length);
                binaryWriter.Write(valueBytes, 0, valueBytes.Length);
                binaryWriter.Flush();

                long requestId =
                    this._RequestHandler.MakeRequest(((MemoryStream)requestStream).ToArray());
                if (requestId == -1) return;

                byte[] responseBytes = this._ResponseHandler.WaitForMessage(requestId);
                if (responseBytes == null || responseBytes.Length == 0)
                    return;

                switch (responseBytes[0])
                {
                    case 0:
                        return;
                    case 1:
                        throw new KeyLockedException();
                    default:
                        throw new DssCommandException();                        
                }
            }
            finally
            {
                binaryWriter?.Close();
                requestStream?.Close();
            }
        }
        
        private string _Lock(string key)
        {
            long requestId;

            // Make Request
            BinaryWriter binaryWriter = null;
            Stream requestStream = null;

            try
            {
                requestStream = new MemoryStream();
                binaryWriter = new BinaryWriter(requestStream);

                /*
                 * -> LCK\BYTE\CHARS{BYTEVALUELENGTH}\BYTE\CHARS{BYTEVALUELENGTH}
                 */

                binaryWriter.Write("LCK".ToCharArray());
                binaryWriter.Write((byte)this.UniqueId.Length);
                binaryWriter.Write(this.UniqueId.ToCharArray());
                binaryWriter.Write((byte)key.Length);
                binaryWriter.Write(key.ToCharArray());
                binaryWriter.Flush();

                requestId = 
                    this._RequestHandler.MakeRequest(((MemoryStream)requestStream).ToArray());
                if (requestId == -1) return null;
            }
            finally
            {
                binaryWriter?.Close();
                requestStream?.Close();
            }

            byte[] responseBytes = 
                this._ResponseHandler.WaitForMessage(requestId);
            if (responseBytes == null || responseBytes.Length == 0)
                return null;
            
            // Parse Response
            BinaryReader binaryReader = null;
            Stream responseStream = null;

            try
            {
                responseStream = 
                    new MemoryStream(responseBytes, 0, responseBytes.Length, false);
                binaryReader = new BinaryReader(responseStream);

                /*
                 * <- \BYTE\CHARS{BYTEVALUELENGTH}\BYTE\BYTE\CHARS{BYTEVALUELENGTH}
                 */
                
                byte remoteKeyLength = binaryReader.ReadByte();
                string remoteKey = 
                    new string(binaryReader.ReadChars(remoteKeyLength));

                byte remoteResult = 
                    binaryReader.ReadByte();

                if (remoteResult == 0)
                {
                    int remoteLockCodeLength = binaryReader.ReadByte();
                    string remoteLockCode =
                        new string(binaryReader.ReadChars(remoteLockCodeLength));

                    return string.CompareOrdinal(remoteKey, key) == 0 
                        ? remoteLockCode 
                        : null;
                }

                if (remoteResult == 1) // KeyLockedException
                {
                    if (string.CompareOrdinal(remoteKey, key) == 0)
                        throw new KeyLockedException();
                    return null;
                }
                
                if (string.CompareOrdinal(remoteKey, key) == 0)
                    throw new DssCommandException();
                return null;
            }
            finally
            {
                binaryReader?.Close();
                responseStream?.Close();
            }
        }

        private void _Release(string key, string lockCode)
        {            
            // Make Request
            BinaryWriter binaryWriter = null;
            Stream requestStream = null;

            try
            {
                requestStream = new MemoryStream();
                binaryWriter = new BinaryWriter(requestStream);

                /*
                 * -> RLS\BYTE\CHARS{BYTEVALUELENGTH}\BYTE\CHARS{BYTEVALUELENGTH}\BYTE\CHARS{BYTEVALUELENGTH}
                 */

                binaryWriter.Write("RLS".ToCharArray());
                binaryWriter.Write((byte)this.UniqueId.Length);
                binaryWriter.Write(this.UniqueId.ToCharArray());
                binaryWriter.Write((byte)key.Length);
                binaryWriter.Write(key.ToCharArray());
                binaryWriter.Write((byte)lockCode.Length);
                binaryWriter.Write(lockCode.ToCharArray());
                binaryWriter.Flush();

                long requestId =
                    this._RequestHandler.MakeRequest(((MemoryStream)requestStream).ToArray());
                if (requestId == -1) return;

                byte[] responseBytes = this._ResponseHandler.WaitForMessage(requestId);
                if (responseBytes == null || responseBytes.Length == 0)
                    return;

                if (responseBytes[0] != 0)
                    throw new DssCommandException();
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
                if (requestId == -1) return keys.ToArray();
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

            byte[] responseBytes = 
                this._ResponseHandler.WaitForMessage(requestId);
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

                BinaryFormatter binFormatter = 
                    new BinaryFormatter {Binder = new Binder(Helpers.Name)};
                binFormatter.Serialize(forStream, value);

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

                BinaryFormatter binFormatter =
                    new BinaryFormatter {Binder = new Binder(Helpers.Name)};
                return binFormatter.Deserialize(forStream);
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

        public bool IsExpired => DateTime.Compare(DateTime.Now, this.Expires) > 0;

        public void Extend()
        {
            // Make Request
            BinaryWriter binaryWriter = null;
            Stream requestStream = null;

            try
            {
                requestStream = new MemoryStream();
                binaryWriter = new BinaryWriter(requestStream);

                /*
                 * -> EXT\BYTE\CHARS{BYTEVALUELENGTH}
                 */

                binaryWriter.Write("EXT".ToCharArray());
                binaryWriter.Write((byte)this.UniqueId.Length);
                binaryWriter.Write(this.UniqueId.ToCharArray());
                binaryWriter.Flush();

                this._RequestHandler.MakeRequest(((MemoryStream)requestStream).ToArray());
            }
            finally
            {
                binaryWriter?.Close();
                requestStream?.Close();
            }
        }
    }
}
