//*************************************************************************
//	创建日期:	2015-6-29
//	文件名称:	NetWork.cs
//  创 建 人:    Rect 	
//	版权所有:	MIT
//	说    明:	网络中心处理
//*************************************************************************

//-------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GameCore.NetWork
{
    
    public class CNetTCPWork : INetWork
    {
        #region Member variables
        private Dictionary<int, CNetTCPSocketConnect> m_TCPConnects;
        private int m_currentConnectedSID;
        private ENUM_SOCKET_STATE m_currentConnectState;

        private int m_ReadyToConnectSID;
        #endregion
        //-------------------------------------------------------------------------
        public CNetTCPWork()
        {
            m_TCPConnects = new Dictionary<int, CNetTCPSocketConnect>();

            DisconnectAll();
        }
        //-------------------------------------------------------------------------
        ~CNetTCPWork()
        {
            DisconnectAll();
        }
        //-------------------------------------------------------------------------
        #region public method
        //-------------------------------------------------------------------------
        /// <summary>
        /// 生命周期内每侦更新
        /// </summary>
        public void Update()
        {
            if (0 == m_TCPConnects.Count)
            {
                return;
            }

            List<CNetTCPSocketConnect> listTemp = new List<CNetTCPSocketConnect>();

            foreach (KeyValuePair<int, CNetTCPSocketConnect> p in m_TCPConnects)
            {
                listTemp.Add(p.Value);
            }

            foreach (CNetTCPSocketConnect c in listTemp)
            {
                __Update(c);
            }

            listTemp.Clear();


        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 创建连接
        /// </summary>
        /// <param name="id"></param>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="listener"></param>
        public void Connect(int id, string host, int port, INetworkMsgHandler listener)
        {
            SetReadyToConnectSID(id);

            if (ENUM_SOCKET_STATE.eSocket_Connected == m_currentConnectState)
            {
                if (id == m_currentConnectedSID)
                {
                    GlobalUtil.Log("NetTCPWork::Connect already connect the server ID = " + id);
                    return;
                }

                CNetTCPSocketConnect connect = new CNetTCPSocketConnect();
                bool success = connect.Connect(id,host, port, listener);
                if (success)
                {
                    SetCurrentServerID(id);
                    m_TCPConnects.Add(id, connect);
                }
            }
            else
            {
                Disconnect(id);

                CNetTCPSocketConnect connect = new CNetTCPSocketConnect();
                bool success = connect.Connect(id,host, port, listener);
                if (success)
                {
                    m_TCPConnects.Add(id, connect);
                }
            }
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 断开连接
        /// </summary>
        /// <param name="id"></param>
        public void Disconnect(int id)
        {
            CNetTCPSocketConnect c = null;

            if (m_TCPConnects.TryGetValue(id, out c))
            {
                if (null != c)
                {
                    GlobalUtil.Log("CNetTCPWork::Disconnect Remove ID = " + id);
                    c.DisConnection();
                    m_TCPConnects[id] = null;
                    if (id == m_currentConnectedSID)
                    {
                        m_currentConnectState = ENUM_SOCKET_STATE.eSocket_DisConnected;
                    }
                }
                m_TCPConnects.Remove(id);
                
            }
            
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 关闭所有连接
        /// </summary>
        public void DisconnectAll()
        {
            foreach (KeyValuePair<int, CNetTCPSocketConnect> p in m_TCPConnects)
            {
                if (null != p.Value)
                {
                    p.Value.DisConnection();
                }

            }
            m_TCPConnects.Clear();
            __Clear();
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 重连当前连接
        /// </summary>
        public void ReConnect()
        {
            CNetTCPSocketConnect c = null;
            if (m_TCPConnects.TryGetValue(m_currentConnectedSID, out c))
            {
                c.DisConnection();
                c.Reconnect();
            }
        }
        //-------------------------------------------------------------------------
        public bool SendMessage(int nMessageID, Byte[] data, int id = -1)
        {
            if (null == data)
            {
                return false;
            }

             // 如果还没刷新 就手动刷新一次
            //if (SNetCommon.NUNE_VALUE == m_currentConnectedSID)
            //{
            //    Update();
            //}

            int tempID = id;
            if (id == SNetCommon.NUNE_VALUE)
            {
                tempID = m_currentConnectedSID;
            }

            // 防止 系统尚未update connect 但是就sendMessage了
            if (id == SNetCommon.NUNE_VALUE)
            {
                tempID = m_ReadyToConnectSID;
            }

            CNetTCPSocketConnect c = null;
            if (m_TCPConnects.TryGetValue(tempID, out c))
            {
                if (null != c || c.IsConnect())
                {
                    c.SendMessage(nMessageID, data);
                    return true;
                }
            }
            GlobalUtil.Log("CNetWork::SendMessage false id = " + id);
            return false;
        }
        //-------------------------------------------------------------------------
        public int GetCurrentServerID()
        {
            return m_currentConnectedSID;
        }
        //-------------------------------------------------------------------------
        public void SetCurrentServerID(int v)
        {
            m_currentConnectedSID = v;
        }
        //-------------------------------------------------------------------------
        public int GetReadyToConnectSID()
        {
            return m_ReadyToConnectSID;
        }
        //-------------------------------------------------------------------------
        public void SetReadyToConnectSID(int v)
        {
            m_ReadyToConnectSID = v;
        }
        //-------------------------------------------------------------------------
        public string ToNetWorkString()
        {
            StringBuilder strBuilder = new StringBuilder();
            strBuilder.Append("have no connect!");
            uint unSendData = 0;
            uint unRecvData = 0;

            CNetTCPSocketConnect c = null;
            if (m_TCPConnects.TryGetValue(m_currentConnectedSID, out c))
            {
                if (null != c )
                {
                    strBuilder.Remove(0, strBuilder.Length);
                    unSendData = c.GetSendTotalBytes();
                    unRecvData = c.GetRectTotalBytes();

                    if (c.IsConnect())
                    {
                        strBuilder.Append("Connect:" + c.GetIP() + ":" + c.GetPort());
                        
                    }
                    else
                    {
                        strBuilder.Append("Connect Failed ");
                    }
                    strBuilder.Append(" - ");
                    strBuilder.Append(unSendData);
                    strBuilder.Append("/");
                    strBuilder.Append(unRecvData);

                }
            }
            return strBuilder.ToString();
        }
        //-------------------------------------------------------------------------
        #endregion

        #region private method
        //-------------------------------------------------------------------------
        private void __Clear()
        {
            m_currentConnectedSID = SNetCommon.NUNE_VALUE;
            m_currentConnectState = ENUM_SOCKET_STATE.eSocket_DisConnected;
            m_ReadyToConnectSID = SNetCommon.NUNE_VALUE;
        }
        //-------------------------------------------------------------------------
        private void __Update(CNetTCPSocketConnect connect)
        {
            if (null == connect)
            {
                return;
            }

            int nID = SNetCommon.NUNE_VALUE;
            // 进入连接器 状态回调
            ENUM_SOCKET_STATE sState = connect.Update(out nID);
            if (nID == m_ReadyToConnectSID)
            {
                m_currentConnectState = sState;
                m_currentConnectedSID = m_ReadyToConnectSID;
            }

            // 消息取出来 外部进行消息分发
            if (connect.IsConnect())
            {
                List<SocketNetPacket> packList = new List<SocketNetPacket>();
                connect.GetAllReceivePack(packList);
                foreach (SocketNetPacket tempack in packList)
                {
                    NetMessageRecieveHandle.GetInstance().OnRecvMessage(tempack);
                }
            }
            
        }
        #endregion
    }
}
