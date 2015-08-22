//*************************************************************************
//	创建日期:	2015-6-29
//	文件名称:	NetConnect.cs
//  创 建 人:    Rect 	
//	版权所有:	MIT
//	说    明:	网络连接器接口
//*************************************************************************

//-------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace GameCore.NetWork
{
    /// <summary>
    /// 网络状态事件基类
    /// </summary>
    public abstract class INetworkMsgHandler//abstract 
    {
        public NetWorkHandDelegate del_OnConnectStart;      //socket开始
        public NetWorkHandDelegate del_OnDisconnect;        //socket意外断开(包括服务端断开命令)的回调函数
        public NetWorkHandDelegate del_OnConnectSuccess;    //socket连接成功的回调函数
        public NetWorkHandDelegate del_Update;              //socket处于连接中的回调函数(暂时没用到)

        public INetworkMsgHandler()
        {
            del_OnDisconnect = new NetWorkHandDelegate(OnDisConnect);
            del_OnConnectSuccess = new NetWorkHandDelegate(OnConnectSuccess);
            del_OnConnectStart = new NetWorkHandDelegate(OnConnectStart);
            del_Update = new NetWorkHandDelegate(OnUpdate);
        }

        public virtual void OnDisConnect(int sID) { }
        public virtual void OnConnectSuccess(int sID) { }
        public virtual void OnConnectStart(int sID) { }
        public virtual void OnUpdate(int sID) { }
    }

    /// <summary>
    /// 连接器接口
    /// </summary>
    public interface  INetConnect
    {
        /// <summary>
        /// 判断连接是否成功
        /// </summary>
        /// <returns></returns>
        bool IsConnect();

        /// <summary>
        /// 关闭连接
        /// </summary>
        void DisConnection();

        /// <summary>
        /// 断线重连
        /// </summary>
        /// <returns></returns>
        bool Reconnect();

        /// <summary>
        /// 连接开始
        /// </summary>
        /// <param name="ip">服务器IP地址</param>
        /// <param name="portnumber">端口信息</param>
        /// <returns></returns>
        bool Connect(int nSocketID, string ip, int portnumber, INetworkMsgHandler listener);

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="nMessageID"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        bool SendMessage(int nMessageID, Byte[] data);

        /// <summary>
        /// 连接器更新
        /// </summary>
        ENUM_SOCKET_STATE Update(out int nID);

        /// <summary>
        /// 获取连接类型 tcp or udp or other
        /// </summary>
        /// <returns></returns>
        ProtocolType GetConnectType();

        /// <summary>
        /// 发送字节数总数
        /// </summary>
        /// <returns></returns>
        uint GetSendTotalBytes();

        /// <summary>
        /// 接收字节数总数
        /// </summary>
        /// <returns></returns>
        uint GetRectTotalBytes();

        /// <summary>
        /// 获取端口
        /// </summary>
        /// <returns></returns>
        int GetPort();

        /// <summary>
        /// 获取IP
        /// </summary>
        /// <returns></returns>
        string GetIP();

    }

    /// <summary>
    /// 消息包接口
    /// </summary>
    public interface INetPacket
    {
        /// <summary>
        /// 清空数据
        /// </summary>
        void Clear();

        /// <summary>
        /// 获取消息号
        /// </summary>
        /// <returns></returns>
        Int16 GetMessageID();

        /// <summary>
        /// 获取消息包数据缓存 - 包括包头和包身
        /// </summary>
        /// <returns></returns>
        Byte[] GetBuffer(out int nBufferSize);

        /// <summary>
        /// 获取消息包体内容 - 包身
        /// </summary>
        /// <returns></returns>
        Byte[] GetBody(out int nBodySize);

        /// <summary>
        /// 设置包头的数据
        /// </summary>
        /// <returns>true or false</returns>
        bool SetPackHead(Byte[] headData);

        /// <summary>
        /// 设置包身的数据
        /// </summary>
        /// <returns>true or false</returns>
        bool SetPackBody(Byte[] bodyData);

        /// <summary>
        /// 获取包大小
        /// </summary>
        /// <returns></returns>
        int GetSize();
    }

    /// <summary>
    /// 网络管理器接口
    /// </summary>
    public interface INetWork
    {
        void Update();
        void Connect(int id, string host, int port, INetworkMsgHandler listener);
        void Disconnect(int id);
        void DisconnectAll();
        void ReConnect();
        bool SendMessage(int command, Byte[] data, int id = -1);
        int GetCurrentServerID();
        void SetCurrentServerID(int v);
        int GetReadyToConnectSID();
        void SetReadyToConnectSID(int v);
        string ToNetWorkString();
    }


}
