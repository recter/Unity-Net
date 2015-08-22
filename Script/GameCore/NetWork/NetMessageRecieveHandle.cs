//*************************************************************************
//	创建日期:	2015-6-29
//	文件名称:	NetMessageRecieveHandle.cs
//  创 建 人:    Rect 	
//	版权所有:	MIT
//	说    明:	网络消息侦听注册器 / 分发器 
//*************************************************************************

//-------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GameCore.NetWork
{
    public class NetMessageRecieveHandle
    {
        //-------------------------------------------------------------------------
        // 单例模式构建
        #region GetInstance - 单例模式构建
        private static NetMessageRecieveHandle p_Instance;
        //-------------------------------------------------------------------------
        static public NetMessageRecieveHandle GetInstance()
        {
            if (p_Instance == null)
            {
                p_Instance = new NetMessageRecieveHandle();
            }
            return p_Instance;
        }
        //-------------------------------------------------------------------------
        private NetMessageRecieveHandle()
        {
            m_MessageHandlers = new Dictionary<int, List<NetOnRecvMessageDelegate>>();
            m_OnRecvMsgCallBack = null;
        }
        //-------------------------------------------------------------------------
        #endregion
        //-------------------------------------------------------------------------
        // 成员变量
        #region Member variables
        // 存放所有消息分发函数的字典
        private Dictionary<int, List<NetOnRecvMessageDelegate>> m_MessageHandlers;
        // 收到消息时的回调处理
        private Action<SocketNetPacket> m_OnRecvMsgCallBack;
        #endregion
        //-------------------------------------------------------------------------
        #region method - 处理函数
        /// <summary>
        /// 注册消息监听
        /// </summary>
        /// <param name="command">消息号ID</param>
        /// <param name="del">消息接收回调委托</param>
        public void RegisterMessageHandler(int command, NetOnRecvMessageDelegate del)
        {
            List<NetOnRecvMessageDelegate> handlerList = null;

            if (m_MessageHandlers.ContainsKey(command))
            {
                m_MessageHandlers.TryGetValue(command, out handlerList);
            }
            else
            {
                handlerList = new List<NetOnRecvMessageDelegate>();
                m_MessageHandlers.Add(command, handlerList);
            }

            if (!handlerList.Contains(del))
            {
                handlerList.Add(del);
            }
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 消息批量监听注册
        /// </summary>
        /// <param name="commands">消息号ID数组</param>
        /// <param name="del">消息回调委托</param>
        public void RegisterMessageHandler(int[] commands, NetOnRecvMessageDelegate del)
        {
            foreach (int c in commands)
            {
                RegisterMessageHandler(c, del);
            }
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 注销消息监听 - 注销消息号指定的委托类
        /// </summary>
        /// <param name="command"></param>
        /// <param name="del"></param>
        public void UnRegisterMessageHandler(int command, NetOnRecvMessageDelegate del)
        {
            if (null == del || 0 >= command)
            {
                return;
            }

            if (m_MessageHandlers.ContainsKey(command))
            {
                List<NetOnRecvMessageDelegate> handlerList = null;
                m_MessageHandlers.TryGetValue(command, out handlerList);
                if (handlerList.Contains(del))
                {
                    handlerList.Remove(del);
                }

                if (0 == handlerList.Count)
                {
                    UnRegisterMessageHandler(command);
                }
            }
            else
            {
                Debug.Log("UnRegisterMessageHandler have no del by command:" + command);
            }
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 注销消息监听 - 注销所有此消息号的监听 (如果消息不是模块独有,不推荐使用次方法 by Rect)
        /// </summary>
        /// <param name="command">消息号ID</param>
        public void UnRegisterMessageHandler(int command)
        {
            m_MessageHandlers.Remove(command);
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 批量注销消息监听 - 注销所有此消息号的监听 (如果消息不是模块独有,不推荐使用次方法 by Rect)
        /// </summary>
        /// <param name="command">消息号ID数组</param>
        public void UnRegisterMessageHandler(int[] commands)
        {
            foreach (int c in commands)
            {
                UnRegisterMessageHandler(c);
            }
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 解包处理
        /// </summary>
        /// <param name="client"></param>
        /// <param name="packet"></param>
        public void OnRecvMessage(SocketNetPacket packet)
        {
            if (m_MessageHandlers.ContainsKey((int)packet.GetMessageID()))
            {
                if (m_OnRecvMsgCallBack != null)
                {
                    m_OnRecvMsgCallBack(packet);
                }

                List<NetOnRecvMessageDelegate> handlerList = null;
                m_MessageHandlers.TryGetValue((int)packet.GetMessageID(), out handlerList);

                if (handlerList != null)
                {
                    List<NetOnRecvMessageDelegate> deletes = new List<NetOnRecvMessageDelegate>();
                    List<NetOnRecvMessageDelegate> exeList = new List<NetOnRecvMessageDelegate>(handlerList);
                    foreach (NetOnRecvMessageDelegate del in exeList)
                    {
                        if (del != null)
                        {
                            if (del.Target != null && del.Target.ToString() != SNetCommon.NULL)
                            {
                                int nSize = 0;
                                Byte[] pBuffer = packet.GetBody(out nSize);
                                int nMessageID = packet.GetMessageID();
                                del(nMessageID, pBuffer, nSize);
                            }
                            else
                            {
                                deletes.Add(del);
                            }
                        }
                    }

                    foreach (NetOnRecvMessageDelegate i in deletes)
                    {
                        handlerList.Remove(i);
                    }
                }
            }
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 设置收到消息时的回调函数
        /// </summary>
        /// <param name="cb"></param>
        public void SetRecvMsgCB(Action<SocketNetPacket> cb)
        {
            m_OnRecvMsgCallBack = cb;
        }
        //-------------------------------------------------------------------------
        #endregion

        
        
    }
}
