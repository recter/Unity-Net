//*************************************************************************
//	创建日期:	2015-6-29
//	文件名称:	NetTCPSocketConnect.cs
//  创 建 人:    Rect 	
//	版权所有:	MIT
//	说    明:	TCP 连接器
//*************************************************************************

//-------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace GameCore.NetWork
{
    public class CNetTCPSocketConnect : INetConnect
    {
        #region Member variables

        private Socket          m_Socket;                       // socket 对象
        private SIPAdressMessage m_IPAddrMsg;                   // 端口 地址对象
        private Byte[]          m_ReceiveHead;                  // 包头buffer...
        private Thread          m_ReceiveThread;                // 接收子线程现场
        private Thread          m_SendThread;                   // 发送子线程现场
        private int             m_nSocketID;                    // 连接ID
        private LinkedList<SocketNetPacket> m_SendPackList;     // 存储发送消息包的队列
        private System.Object               m_SendObject;       // 发送消息包的互斥锁对像    

        private uint                        m_unSendTotalBytes; // 发送数据总和
        private uint                        m_unRecvTotalBytes; // 接收数据总和        
       
        private LinkedList<SocketNetPacket> m_ReceivePackList;  // 存储哦接收的消息包的队列
        private System.Object               m_ReceiveObject;    // 接收包的互斥锁对像

        // Note:AutoResetEvent只会给一个线程发送信号，而不会给多个线程发送信号。在我们需要同步多个线程的时候，就用ManualResetEvent.
        private ManualResetEvent            m_ManualSendEvent;  // 发送消息的线程状态
        private AutoResetEvent              m_AutoConnectEvent; // 用于控制异步创建连接的时候等待创建完成

        private INetworkMsgHandler          m_NetStateListener;
        #endregion
        //-------------------------------------------------------------------------
        /// <summary>
        /// 构造函数
        /// </summary>
        public CNetTCPSocketConnect()
        {
            __Clear();
            m_AutoConnectEvent = new AutoResetEvent(false);

        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 析构函数
        /// </summary>
        ~CNetTCPSocketConnect() 
        {
            __Clear();
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 变量初始化
        /// </summary>
        private void __Clear()
        {
            m_Socket = null;
            m_IPAddrMsg.Clear();

            m_ReceiveHead = null;

            __DestoryNetWorkThread();

            if (null != m_SendPackList)
            {
                m_SendPackList.Clear();
            }
            m_SendObject = null;

            if (null != m_ReceivePackList)
            {
                m_ReceivePackList.Clear();
            }
            m_ReceiveObject = null;

            if (null != m_ManualSendEvent)
            {
                m_ManualSendEvent.Close();
                m_ManualSendEvent = null;
            }

            if (null != m_AutoConnectEvent)
            {
                m_AutoConnectEvent.Close();
                m_AutoConnectEvent = null;
            }

            m_NetStateListener = null;

            m_unSendTotalBytes = 0;
            m_unRecvTotalBytes = 0;
            
        }
        //-------------------------------------------------------------------------
        #region InterFace - NetConnect
        /// <summary>
        /// 判断连接是否成功
        /// </summary>
        /// <returns></returns>
        public bool IsConnect()
        {
            return m_IPAddrMsg.m_IsConnect;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 关闭连接
        /// </summary>
        public void DisConnection()
        {
            m_IPAddrMsg.m_IsNeedClose = false;

            __DestoryNetWorkThread();

            if (m_Socket != null)
            {
                try
                {
                    m_IPAddrMsg.m_IsNeedCallDisConnect = true;
                    m_Socket.Shutdown(SocketShutdown.Both);
                    m_Socket.Close();
                    Debug.Log("NetTCPSocketConnect::DisConnection - close socket");
                }
                catch (System.Exception excep)
                {
                    Debug.LogError("NetTCPSocketConnect::DisConnection - closet socket eror:" + excep.ToString());
                }
            }

            m_Socket = null;
            m_IPAddrMsg.m_IsConnect = false;

            if (null != m_NetStateListener)
            {
                m_NetStateListener.del_OnDisconnect(m_nSocketID);
            }
            
            return;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 断线重连
        /// </summary>
        /// <returns></returns>
        public bool Reconnect()
        {
            return Connect(m_nSocketID,m_IPAddrMsg.m_IP, m_IPAddrMsg.m_Port, m_NetStateListener);
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 连接开始
        /// </summary>
        /// <param name="ip">服务器IP地址</param>
        /// <param name="portnumber">端口信息</param>
        /// <returns></returns>
        public bool Connect(int nSocketID, string ip, int portnumber, INetworkMsgHandler listener)
        {
            if (m_IPAddrMsg.m_IsConnect)
            {
                DisConnection();
            }

            if (0 == ip.Length || ip.Equals(SNetCommon.NULL) || 0 >= portnumber)
            {
                Debug.Log("NetTCPSocketConnect::Connect arge is error");
                return false;
            }
            m_nSocketID = nSocketID;
            m_IPAddrMsg.m_IP = ip;
            m_IPAddrMsg.m_Port = portnumber;

            m_NetStateListener = listener;

            // 获取 DNS 主机地址
            //  DNS 服务器中查询与某个主机名关联的 IP 地址。 如果 hostNameOrAddress 是 IP 地址，则不查询 DNS 服务器直接返回此地址
            IPAddress[] addresses = null;
            try
            {
                addresses = Dns.GetHostAddresses(ip);
            }
            catch (System.Exception excep)
            {
                Debug.Log("NetTCPSocketConnect::Connect DNS Error:" + excep.ToString());
                return false;
            }

            if (addresses == null || addresses.Length == 0)
            {
                Debug.Log("NetTCPSocketConnect::Connect addresses:" + addresses.ToString());
                return false;

            }
            // 将网络端点表示为 IP 地址和端口号
            IPEndPoint remoteIP = new IPEndPoint(addresses[0], portnumber);

            if (null != m_NetStateListener)
            {
                m_NetStateListener.del_OnConnectStart(nSocketID);
            }

            ///建立一个TCP连接
            m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                Debug.Log("NetTCPSocketConnect::Connect begin connect to server:" + ip + " port:" + portnumber.ToString());
                m_IPAddrMsg.m_IsConnect = false;

                /* 
                 * SocketAsyncEventArgs是微软提供的高性能异步Socket实现类，主要为高性能网络服务器应用程序而设计，主要是为了避免在在异步套接字 I/O 
                 * 量非常大时发生重复的对象分配和同步。使用此类执行异步套接字操作的模式包含以下步骤：
                 *   1.分配一个新的 SocketAsyncEventArgs 上下文对象，或者从应用程序池中获取一个空闲的此类对象。
                 *   2.将该上下文对象的属性设置为要执行的操作（例如，完成回调方法、数据缓冲区、缓冲区偏移量以及要传输的最大数据量）。
                 *   3.调用适当的套接字方法 (xxxAsync) 以启动异步操作。
                 *   4.如果异步套接字方法 (xxxAsync) 返回 true，则在回调中查询上下文属性来获取完成状态。
                 *   5.如果异步套接字方法 (xxxAsync) 返回 false，则说明操作是同步完成的。 可以查询上下文属性来获取操作结果。
                 *   6.将该上下文重用于另一个操作，将它放回到应用程序池中，或者将它丢弃。
                 */
                SocketAsyncEventArgs asyncEvent = new SocketAsyncEventArgs();
                asyncEvent.RemoteEndPoint = remoteIP;
                asyncEvent.Completed += new EventHandler<SocketAsyncEventArgs>(__OnConnectComplete);
                m_Socket.ConnectAsync(asyncEvent);
                // 等待信号
                m_AutoConnectEvent.WaitOne();

                SocketError errorCode = asyncEvent.SocketError;
                if (errorCode != SocketError.Success)
                {
                    //throw new SocketException((Int32)errorCode);
                    Debug.Log("NetTCPSocketConnect::Connect check the net SocketError = " + errorCode);
                }
            }
            catch (System.Exception e)
            {
                Debug.Log("NetTCPSocketConnect::Connect " + e.ToString());
                return false;
            }

            return true;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="nMessageID"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool SendMessage(int nMessageID, Byte[] data)
        {
            if (null == data)
            {
                return false;
            }

            int bodysize = data.Length;
            ///组包
            SocketNetPacket sendPack = new SocketNetPacket((Int16)nMessageID, bodysize);
            sendPack.SetPackBody(data);

            lock (m_SendObject)
            {
                m_SendPackList.AddLast(sendPack);
                if (1 == m_SendPackList.Count)
                {
                    m_ManualSendEvent.Set();
                }
                
                Debug.Log("CNetTCPSocketConnect::SendMessage m_SendPackList.Count = " + m_SendPackList.Count);
            }

            return true;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 连接器更新
        /// </summary>
        public ENUM_SOCKET_STATE Update(out int nID)
        {
            ENUM_SOCKET_STATE sState = ENUM_SOCKET_STATE.eSocket_DisConnected;

            nID = m_nSocketID;

            if (null != m_NetStateListener)
            {
                if (m_IPAddrMsg.m_IsNeedCallConnected)
                {
                    sState = ENUM_SOCKET_STATE.eSocket_Connected;
                    m_NetStateListener.del_OnConnectSuccess(m_nSocketID);
                    m_IPAddrMsg.m_IsNeedCallConnected = false;
                }
                else if (m_IPAddrMsg.m_IsNeedCallDisConnect)
                {
                    sState = ENUM_SOCKET_STATE.eSocket_DisConnected;
                    m_NetStateListener.del_OnDisconnect(m_nSocketID);
                    m_IPAddrMsg.m_IsNeedCallDisConnect = false;
                }
            }

            if (m_IPAddrMsg.m_IsNeedClose)
            {
                DisConnection();
                return sState;
            }

            if (null != m_NetStateListener)
            {
                m_NetStateListener.del_Update(m_nSocketID);
            }

            return sState;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 获取连接类型 tcp or udp or other
        /// </summary>
        /// <returns></returns>
        public ProtocolType GetConnectType()
        {
            return ProtocolType.Tcp;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 发送字节数总数
        /// </summary>
        /// <returns></returns>
        public uint GetSendTotalBytes()
        {
            return m_unSendTotalBytes;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 接收字节数总数
        /// </summary>
        /// <returns></returns>
        public uint GetRectTotalBytes()
        {
            return m_unRecvTotalBytes;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 获取端口
        /// </summary>
        /// <returns></returns>
        public int GetPort()
        {
            return m_IPAddrMsg.m_Port;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 获取IP
        /// </summary>
        /// <returns></returns>
        public string GetIP()
        {
            return m_IPAddrMsg.m_IP;
        }
        #endregion

        #region public method
        //-------------------------------------------------------------------------
        /// <summary>
        /// 取出消息缓存中所有接收到的消息队列，并清空消息缓存
        /// </summary>
        /// <param name="packList"></param>
        public void GetAllReceivePack(List<SocketNetPacket> packList)
        {
            packList.Clear();

            if (null == m_ReceiveObject)
            {
                return;
            }

            // lock 
            lock (m_ReceiveObject)
            {
                if (0 < m_ReceivePackList.Count)
                {
                    foreach (SocketNetPacket temPack in m_ReceivePackList)
                    {
                        packList.Add(temPack);
                    }
                    m_ReceivePackList.Clear();
                }

            }
            // unlock 
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 取出消息缓存中排在最前的一条的消息包的数据，并从消息缓存中移除
        /// </summary>
        /// <returns></returns>
        public SocketNetPacket GetReceivePack()
        {
            if (m_ReceiveObject == null)
            {
                return null;
            }
            SocketNetPacket pack = null;
            // lock 
            lock (m_ReceiveObject)
            {
                if (0 < m_ReceivePackList.Count)
                {
                    pack = m_ReceivePackList.First.Value;
                    m_ReceivePackList.RemoveFirst();
                }
            }
            // unlock 
            return pack;
        }
        //-------------------------------------------------------------------------
        #endregion

        #region private method
        //-------------------------------------------------------------------------
        /// <summary>
        /// 异步创建连接成功回调
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void __OnConnectComplete(object sender, SocketAsyncEventArgs e)
        {

            // 释放现成信号.让主继续进行
            m_AutoConnectEvent.Set();
            if (e.SocketError == SocketError.Success)
            {
                Debug.Log("NetTCPSocketConnect::__OnConnectComplete handle socket conect to server success");

                m_IPAddrMsg.m_IsConnect = true;
                __CreateNetWorkThread();
                m_IPAddrMsg.m_IsNeedCallConnected = true;
                m_IPAddrMsg.m_IsNeedCallDisConnect = false;
            }
            else
            {
                Debug.LogError("NetTCPSocketConnect::__OnConnectComplete handle socket conect to server Failed");

                m_IPAddrMsg.m_IsConnect = false;
                m_IPAddrMsg.m_IsNeedCallConnected = false;
                m_IPAddrMsg.m_IsNeedCallDisConnect = true;
            }

        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 创建子线程 用于 读取 和 发送数据 本函数只调用一次
        /// </summary>
        /// <returns></returns>
        private bool __CreateNetWorkThread()
        {
            // 初始化发送队列元素
            m_SendPackList = new LinkedList<SocketNetPacket>();
            m_SendObject = new System.Object();
            // 初始化接收队列元素
            m_ReceivePackList = new LinkedList<SocketNetPacket>();
            m_ReceiveObject = new System.Object();
            // 初始化信号对象
            m_ManualSendEvent = new ManualResetEvent(false);

            // 创建子线程
            if (null == m_ReceiveThread)
            {
                m_ReceiveThread = new Thread(__RectiveThreadFunc);
                m_ReceiveThread.Start();
            }

            if (null == m_SendThread)
            {
                m_SendThread = new Thread(__SendThreadFunc);
                m_SendThread.Start();
            }


            return true;
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 停止子线程
        /// </summary>
        private void __DestoryNetWorkThread()
        {
            // 停止子线程
            if (null != m_ReceiveThread)
            {
                if (m_ReceiveThread.IsAlive)
                {
                    m_ReceiveThread.Abort();
                }
                m_ReceiveThread = null;
            }

            if (null != m_SendThread)
            {
                if (m_SendThread.IsAlive)
                {
                    m_SendThread.Abort();
                }
                m_SendThread = null;
            }
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 接收子线程回调函数
        /// </summary>
        private void __RectiveThreadFunc()
        {
            m_ReceiveHead = new Byte[SNetPacketCommon.PACK_HEAD_SIZE];

            while (null != m_Socket && false == m_IPAddrMsg.m_IsNeedClose)
            {
                Debug.Log("NetTCPSocketConnect::__RectiveThreadFunc at while");
                bool b = false;
                b = __ReadPacketHead();
                if (false == b)
                {

                    Debug.LogError("NetTCPSocketConnect::__RectiveThreadFunc read packhead error will close connect");
                    m_IPAddrMsg.m_IsNeedClose = true;
                    break;
                }

                b = __ReadPacketBody();
                if (false == b)
                {

                    Debug.LogError("NetTCPSocketConnect::__RectiveThreadFunc read packbody error will close connect");
                    m_IPAddrMsg.m_IsNeedClose = true;
                    break;
                }
            }
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 读取 包头数据
        /// </summary>
        /// <returns></returns>
        private bool __ReadPacketHead()
        {
            
            if (null == m_Socket || false == m_Socket.Connected)
            {
                Debug.Log("NetTCPSocketConnect::__ReadPacketHead socket is null or connecd = false ");
                return false;
            }

            try
            {
                Array.Clear(m_ReceiveHead, 0, m_ReceiveHead.Length);
                int receiveSize = m_Socket.Receive(m_ReceiveHead, SNetPacketCommon.PACK_HEAD_SIZE, SocketFlags.None);
                if (receiveSize == 0)
                {
                    Debug.Log("NetTCPSocketConnect::__ReadPacketHead read pack head 0 data ,will close connect");
                    return false;
                }

                while (receiveSize < SNetPacketCommon.PACK_HEAD_SIZE)
                {
                    if (m_Socket == null || m_Socket.Connected == false)
                    {
                        Debug.Log("NetTCPSocketConnect::__ReadPacketHead socket is null or connecd = false ");
                        return false;
                    }
                    
                    int nTemsendcount = m_Socket.Receive(m_ReceiveHead, receiveSize, SNetPacketCommon.PACK_HEAD_SIZE - receiveSize, SocketFlags.None);
                    if (0 == nTemsendcount)
                    {
                        Debug.Log("NetTCPSocketConnect::__ReadPacketHead read pack receive 0 data,will close connect");
                        return false;
                    }

                    receiveSize += nTemsendcount;
                }

                m_unRecvTotalBytes += (uint)receiveSize;

                if (false == SocketNetPacket.IsPackHead(m_ReceiveHead))
                {
                    Debug.Log("NetTCPSocketConnect::__ReadPacketHead receive data is not packhead");
                    return false;
                }

                return true;

            }

            catch (System.Exception e)
            {
                Debug.LogError("NetTCPSocketConnect::__ReadPacketHead Socket  receive error: " + e.ToString());
                return false;
            }

        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 读取包体数据
        /// </summary>
        /// <returns></returns>
        private bool __ReadPacketBody()
        {
            
            if (m_ReceiveHead == null)
            {
                Debug.LogError("NetTCPSocketConnect::__ReadPacketBody m_ReceiveHead is null");
                return false;
            }

            // 获取消息头里的消息id和包身上度
            Int16 buffersize = BitConverter.ToInt16(m_ReceiveHead, SNetPacketCommon.PACK_LENGTH_OFFSET);
            Int16 messageid = BitConverter.ToInt16(m_ReceiveHead, SNetPacketCommon.PACK_MESSSAGEID_OFFSET);

            Int32 bodysize = buffersize - SNetPacketCommon.PACK_HEAD_SIZE;
            if (bodysize <= 0)
            {
                Debug.Log("NetTCPSocketConnect::__ReadPacketBody receive empty pack message id:" + messageid);
                return true;
            }

            SocketNetPacket netPacket = new SocketNetPacket(messageid, bodysize);
            ///设置包头数据
            if (false == netPacket.SetPackHead(m_ReceiveHead))
            {
                Debug.Log("NetTCPSocketConnect::__ReadPacketBody receive headis error");
                return false;
            }

            int nBufferSize = 0;
            Byte[] packBuffer = netPacket.GetBuffer(out nBufferSize);

            try
            {
                if (null == m_Socket || false == m_Socket.Connected)
                {
                    Debug.Log("NetTCPSocketConnect::__ReadPacketBody m_Socket==null|| m_Socket.Connected==fales ");
                    return false;
                }

                Int32 receiveSize = m_Socket.Receive(packBuffer, SNetPacketCommon.PACK_HEAD_SIZE, bodysize, SocketFlags.None);
                if (receiveSize == 0)
                {
                    Debug.Log("NetTCPSocketConnect::__ReadPacketBody readPackBody read 0 data will close  connect");
                    return false;
                }
                // 当要接受的数据超过包的大小时，将消息截断，再接收
                while (receiveSize < bodysize)
                {
                    if (null == m_Socket || false == m_Socket.Connected)
                    {
                        Debug.Log("NetTCPSocketConnect::__ReadPacketBody m_Socket==null|| m_Socket.Connected==falsewww eeee");
                        return false;
                    }


                    Int32 temsendcount = m_Socket.Receive(packBuffer, receiveSize + SNetPacketCommon.PACK_HEAD_SIZE, bodysize - receiveSize, SocketFlags.None);

                    if (temsendcount == 0)
                    {
                        Debug.Log("NetTCPSocketConnect::__ReadPacketBody readPackBody read 0 data will close  connect");
                        return false;
                    }

                    receiveSize += temsendcount;
                }

                m_unRecvTotalBytes += (uint)receiveSize;
                // 加入收包的消息队列
                // lock is begin in here
                lock (m_ReceiveObject)
                {
                    Debug.Log("NetTCPSocketConnect::__ReadPacketBody Recv Full Data Len = " + nBufferSize);
                    m_ReceivePackList.AddLast(netPacket);
                }
                // unlock is done in here

                return true;

            }
            catch (System.Exception e)
            {
                Debug.LogError("NetTCPSocketConnect::__ReadPacketBody  Socket  receive error: " + e.ToString());
                DisConnection();
                return false;
            }
        }
        //-------------------------------------------------------------------------
        /// <summary>
        /// 发送子线程回调函数
        /// </summary>
        private void __SendThreadFunc()
        {
            while (false == m_IPAddrMsg.m_IsNeedClose && true == m_IPAddrMsg.m_IsConnect && null != m_Socket)
            {
                SocketNetPacket sendpack = null;

                //取出消息缓存中最前一条数据
                lock (m_SendObject)
                {
                    if (m_SendPackList.Count != 0)
                    {
                        sendpack = m_SendPackList.First.Value;
                        m_SendPackList.RemoveFirst();
                    }
                }

                //发包给服务器
                if (null != sendpack)
                {
                    try
                    {
                        int nBufferSize = 0;
                        Byte[] sendBuffer = sendpack.GetBuffer(out nBufferSize);

                        int sendsize = m_Socket.Send(sendBuffer, 0, nBufferSize, SocketFlags.None);
                        if (sendsize != nBufferSize)
                        {
                            Debug.LogError("NetTCPSocketConnect::__SendThreadFunc send buffer size error:");
                            m_IPAddrMsg.m_IsNeedClose = true;
                            break;
                        }

                        m_unSendTotalBytes += (uint)sendsize;

                    }
                    catch (System.Exception exception)
                    {
                        Debug.LogError("NetTCPSocketConnect::__SendThreadFunc send buffer size error:" + exception.ToString());
                        m_IPAddrMsg.m_IsNeedClose = true;
                        break;
                    }

                }
                else
                {
                    m_ManualSendEvent.WaitOne();
                }
            }

            if (null == m_Socket)
            {
                Debug.Log("NetTCPSocketConnect::__SendThreadFunc m_Socket is null");
            }
            Debug.Log("NetTCPSocketConnect::__SendThreadFunc m_IPAddrMsg.m_IsNeedClose = " + m_IPAddrMsg.m_IsNeedClose);
            Debug.Log("NetTCPSocketConnect::__SendThreadFunc m_IPAddrMsg.m_IsConnect = " + m_IPAddrMsg.m_IsConnect);
            Debug.Log("NetTCPSocketConnect::__SendThreadFunc send message thread is exit...");
        }
        //-------------------------------------------------------------------------
        #endregion
    }
}
