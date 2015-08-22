//*************************************************************************
//	创建日期:	2015-6-29
//	文件名称:	NetCommon.cs
//  创 建 人:    Rect 	
//	版权所有:	MIT
//	说    明:	
//*************************************************************************

//-------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameCore.NetWork;


namespace GameCore.NetWork
{
    //-------------------------------------------------------------------------
    public enum ENUM_SOCKET_STATE //scoket连接状态
    {
        eSocket_Connected,
        eSocket_DisConnected
    }
    //-------------------------------------------------------------------------
    /// <summary>
    /// 缓存服务器地址（IP、端口号）
    /// </summary>
    public struct SIPAdressMessage
    {
        public string m_IP;                //IP
        public int    m_Port;              //端口号
        public bool   m_IsConnect;         // 是否连接中
        public bool   m_IsNeedCallConnected;
        public bool   m_IsNeedCallDisConnect;
        public bool   m_IsNeedClose;
        

        public void Clear()
        {
            m_IP = "";               //IP
            m_Port = 0;              //端口号
            m_IsConnect = false;     // 是否连接中
            m_IsNeedCallConnected = false;
            m_IsNeedCallDisConnect = false;
            m_IsNeedClose = false;

        }
    }
    //-------------------------------------------------------------------------
    /// <summary>
    /// 接收消息委托类
    /// </summary>
    /// <param name="packet"></param>
    public delegate void NetOnRecvMessageDelegate(int nMessageID,Byte[] pBuffer,int nSize);
    //-------------------------------------------------------------------------
    /// <summary>
    /// 网络连接状态委托类
    /// </summary>
    /// <param name="sID"></param>
    public delegate void NetWorkHandDelegate(int sID);
    //-------------------------------------------------------------------------
    public struct SNetCommon
    {
        public static string NULL = "null";
        public static int NUNE_VALUE = -1;
    }
    //-------------------------------------------------------------------------
    /// <summary>
    /// 消息包公共定义数据
    /// </summary>
    public struct SNetPacketCommon
    {
        public static int PACK_INDEX_0 = 0;             // 第一个字节位置
        public static int PACK_INDEX_1 = 1;             // 第二个字节位置
        public static Byte PACKHEAD0 = 0x08;		    // 包头信息固定值
        public static Byte PACKHEAD1 = 8;		        // 包头信息固定值
        public static int PACK_HEAD_OFFSET = 0;		    // 包头信息（两个字节：<0，1>）
        public static int PACK_LENGTH_OFFSET = 2;		// 消息包长度信息（2个字节：<2,3>）
        public static int PACK_MESSSAGEID_OFFSET = 4;	// 消息id（2个字节：<4,5>）
        public static int PACK_MESSAGE_OFFSET = 6;	    // 包体数据;
        public static int PACK_HEAD_SIZE = 6;           // 定义包头大小
    }
}
