//*************************************************************************
//	创建日期:	2015-6-29
//	文件名称:	NetPacket.cs
//  创 建 人:    Rect 	
//	版权所有:	MIT
//	说    明:	包消息封装类
//*************************************************************************

//-------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GameCore.NetWork
{
    
    public class SocketNetPacket : INetPacket
    {
        #region Member variables
        private Int16   m_n16MessageID;    // 网络包id 2字节 0~65535 足够了
        private Byte[]  m_Buffer;          // 网络包数括体，包头加包身
        #endregion

        #region 构造函数
        public SocketNetPacket(Int16 nMessageID, int bodysize)
        {
            m_n16MessageID = nMessageID;
            m_Buffer = new Byte[bodysize + SNetPacketCommon.PACK_HEAD_SIZE + 1];
            m_Buffer[bodysize + SNetPacketCommon.PACK_HEAD_SIZE] = 0;
            
            Int16 n16Temp = 0;
            Byte[] byteArray = null;

            // 强制固定两个字节标识 0 1 
            m_Buffer[SNetPacketCommon.PACK_INDEX_0] = SNetPacketCommon.PACKHEAD0;
            m_Buffer[SNetPacketCommon.PACK_INDEX_1] = SNetPacketCommon.PACKHEAD1;

            // 复制包头和包身的大小 2 3 
            n16Temp = (Int16)(bodysize + SNetPacketCommon.PACK_HEAD_SIZE);
            byteArray = BitConverter.GetBytes(n16Temp);
            Array.Copy(byteArray, 0, m_Buffer, SNetPacketCommon.PACK_LENGTH_OFFSET, byteArray.Length);

            // 复制消息枚举id的大小 4 5
            n16Temp = nMessageID;
            byteArray = BitConverter.GetBytes(n16Temp);
            Array.Copy(byteArray, 0, m_Buffer, SNetPacketCommon.PACK_MESSSAGEID_OFFSET, byteArray.Length);

        }
        #endregion
        #region interface NetPacket
        /// <summary>
        /// 清空数据
        /// </summary>
        public void Clear()
        {
            m_n16MessageID = -1;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 获取消息号
        /// </summary>
        /// <returns></returns>
        public Int16 GetMessageID()
        {
            return m_n16MessageID;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 获取消息包数据缓存 - 包括包头和包身
        /// </summary>
        /// <returns></returns>
        public Byte[] GetBuffer(out int nBufferSize)
        {
            if (null == m_Buffer)
            {
                GlobalUtil.Log("SocketNetPacket::GetBuffer error m_Buffer is null");
                nBufferSize = 0;
                return null;
            }

            // -1 是创建整包数据的时候加入的 一个字节 '\0' 
            nBufferSize = m_Buffer.Length - 1;

            return m_Buffer;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 获取消息包体内容 - 包身
        /// </summary>
        /// <returns></returns>
        public Byte[] GetBody(out int nBodySize)
        {
            if (null == m_Buffer || m_Buffer.Length <= SNetPacketCommon.PACK_HEAD_SIZE)
            {
                GlobalUtil.Log("SocketNetPacket::GetBody error  m_buffer == null or  buffer.Length <= PACK_HEAD_SIZE");
                nBodySize = 0;
                return null;
            }

            ///获得包身的数据
            Byte[] data = new Byte[m_Buffer.Length - SNetPacketCommon.PACK_HEAD_SIZE];
            Array.Copy(m_Buffer, SNetPacketCommon.PACK_HEAD_SIZE, data, 0, data.Length);
            // -1 是创建整包数据的时候加入的 一个字节 '\0' 
            nBodySize = m_Buffer.Length - SNetPacketCommon.PACK_HEAD_SIZE - 1;
            return data;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 设置包头的数据
        /// </summary>
        /// <returns>true or false</returns>
        public bool SetPackHead(Byte[] headData)
        {
            if (null == headData || headData.Length != SNetPacketCommon.PACK_HEAD_SIZE)
            {
                GlobalUtil.Log("SocketNetPacket::SetPackHead error  headData.Length = " + headData.Length);
                return false;
            }

            if (null == m_Buffer || m_Buffer.Length < SNetPacketCommon.PACK_HEAD_SIZE)
            {
                GlobalUtil.Log("SocketNetPacket::SetPackHead error  m_Buffer.Length = " + m_Buffer.Length);
                return false;
            }

            Array.Copy(headData, 0, m_Buffer, 0, headData.Length);

            return true;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 设置包身的数据
        /// </summary>
        /// <returns>true or false</returns>
        public bool SetPackBody(Byte[] bodyData)
        {
            if (null == bodyData)
            {
                return false;
            }
            if (null == m_Buffer || m_Buffer.Length < bodyData.Length + SNetPacketCommon.PACK_HEAD_SIZE)
            {
                return false;
            }

            Array.Copy(bodyData, 0, m_Buffer, SNetPacketCommon.PACK_MESSAGE_OFFSET, bodyData.Length);

            return true;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 获取包大小
        /// </summary>
        /// <returns></returns>
        public int GetSize()
        {
            if (m_Buffer == null)
            {
                return 0;
            }
            else
            {
                return m_Buffer.Length - 1;
            }
        }
        //-------------------------------------------------------------------------
        #endregion

        /// <summary>
        /// 判断是否是包头数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static bool IsPackHead(Byte[] data)
        {
            if (null == data)
            {
                return false;
            }

            if (data.Length != SNetPacketCommon.PACK_HEAD_SIZE)
            {
                GlobalUtil.LogError("SocketNetPacket::IsPackHead pack head lenght error:" + data.Length);
                return false;
            }

            if (
                data[SNetPacketCommon.PACK_INDEX_0] != SNetPacketCommon.PACKHEAD0 ||
                data[SNetPacketCommon.PACK_INDEX_1] != SNetPacketCommon.PACKHEAD1
                )
            {
                Int16 n16MessageID = BitConverter.ToInt16(data, SNetPacketCommon.PACK_MESSSAGEID_OFFSET);
                GlobalUtil.LogError("SocketNetPacket::IsPackHead pack head  head version is error: messge id:" + n16MessageID);

                string buffer = BitConverter.ToString(data, 0);
                GlobalUtil.LogError("SocketNetPacket::IsPackHead packhead buffer:" + buffer);

                return false;
            }

            return true;
        }
        //-------------------------------------------------------------------------
        
    }
}
