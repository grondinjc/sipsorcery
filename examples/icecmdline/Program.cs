﻿//-----------------------------------------------------------------------------
// Filename: Program.cs
//
// Description: A console application to test the ICE negotiation process.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// 
// History:
// 28 Mar 2020	Aaron Clauson	Created, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

// If uncommented the logic to do the DTLS handshake will be called.
//#define DTLS_IS_ENABLED

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;
using SIPSorcery.Net;
using SIPSorcery.Sys;
using WebSocketSharp;
using WebSocketSharp.Net.WebSockets;
using WebSocketSharp.Server;

namespace SIPSorcery.Examples
{
    public class WebRtcClient : WebSocketBehavior
    {
        public RTCPeerConnection PeerConnection;

        public event Func<WebSocketContext, Task<RTCPeerConnection>> WebSocketOpened;
        public event Func<WebSocketContext, RTCPeerConnection, string, Task> OnMessageReceived;

        public WebRtcClient()
        { }

        protected override void OnMessage(MessageEventArgs e)
        {
            OnMessageReceived(this.Context, PeerConnection, e.Data);
        }

        protected override async void OnOpen()
        {
            base.OnOpen();
            PeerConnection = await WebSocketOpened(this.Context);
        }
    }

    class Program
    {
        private const string WEBSOCKET_CERTIFICATE_PATH = "certs/localhost.pfx";
        private const string DTLS_CERTIFICATE_PATH = "certs/localhost.pem";
        private const string DTLS_KEY_PATH = "certs/localhost_key.pem";
        private const string DTLS_CERTIFICATE_FINGERPRINT = "sha-256 C6:ED:8C:9D:06:50:77:23:0A:4A:D8:42:68:29:D0:70:2F:BB:C7:72:EC:98:5C:62:07:1B:0C:5D:CB:CE:BE:CD";
        private const int WEBSOCKET_PORT = 8081;
        private const string SIPSORCERY_STUN_SERVER = "turn:sipsorcery.com"; //"stun.sipsorcery.com";
        private const string SIPSORCERY_STUN_SERVER_USERNAME = "aaron"; //"stun.sipsorcery.com";
        private const string SIPSORCERY_STUN_SERVER_PASSWORD = "password"; //"stun.sipsorcery.com";

        private static Microsoft.Extensions.Logging.ILogger logger = SIPSorcery.Sys.Log.Logger;

        private static WebSocketServer _webSocketServer;

        static void Main()
        {
            Console.WriteLine("ICE Console Test Program");
            Console.WriteLine("Press ctrl-c to exit.");

            if (!File.Exists(DTLS_CERTIFICATE_PATH))
            {
                throw new ApplicationException($"The DTLS certificate file could not be found at {DTLS_CERTIFICATE_PATH}.");
            }
            else if (!File.Exists(DTLS_KEY_PATH))
            {
                throw new ApplicationException($"The DTLS key file could not be found at {DTLS_KEY_PATH}.");
            }

            // Plumbing code to facilitate a graceful exit.
            CancellationTokenSource exitCts = new CancellationTokenSource(); // Cancellation token to stop the SIP transport and RTP stream.
            ManualResetEvent exitMre = new ManualResetEvent(false);

            AddConsoleLogger();

            RTCConfiguration pcConfiguration = new RTCConfiguration
            {
                iceServers = new List<RTCIceServer> { new RTCIceServer { urls = SIPSORCERY_STUN_SERVER, 
                    username = SIPSORCERY_STUN_SERVER_USERNAME,
                    credential = SIPSORCERY_STUN_SERVER_PASSWORD,
                    credentialType = RTCIceCredentialType.password} }
            };

            var peerConnection = new RTCPeerConnection(pcConfiguration);

            // Start web socket.
            //Console.WriteLine("Starting web socket server...");
            //_webSocketServer = new WebSocketServer(IPAddress.Any, WEBSOCKET_PORT, true);
            //_webSocketServer.SslConfiguration.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(WEBSOCKET_CERTIFICATE_PATH);
            //_webSocketServer.SslConfiguration.CheckCertificateRevocation = false;
            ////_webSocketServer.Log.Level = WebSocketSharp.LogLevel.Debug;
            //_webSocketServer.AddWebSocketService<WebRtcClient>("/sendoffer", (client) =>
            //{
            //    client.WebSocketOpened += SendOffer;
            //    client.OnMessageReceived += WebSocketMessageReceived;
            //});
            //_webSocketServer.AddWebSocketService<WebRtcClient>("/receiveoffer", (client) =>
            //{
            //    client.WebSocketOpened += ReceiveOffer;
            //    client.OnMessageReceived += WebSocketMessageReceived;
            //});
            //_webSocketServer.Start();

            //Console.WriteLine($"Waiting for browser web socket connection to {_webSocketServer.Address}:{_webSocketServer.Port}...");

            // Ctrl-c will gracefully exit the call at any point.
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                exitMre.Set();
            };

            // Wait for a signal saying the call failed, was cancelled with ctrl-c or completed.
            exitMre.WaitOne();

            //_webSocketServer.Stop();
        }

        private static Task<RTCPeerConnection> ReceiveOffer(WebSocketContext context)
        {
            logger.LogDebug($"Web socket client connection from {context.UserEndPoint}, waiting for offer...");

            var peerConnection = CreatePeerConnection(context);

            return Task.FromResult(peerConnection);
        }

        private static async Task<RTCPeerConnection> SendOffer(WebSocketContext context)
        {
            logger.LogDebug($"Web socket client connection from {context.UserEndPoint}, sending offer.");

            var peerConnection = CreatePeerConnection(context);

            var offerInit = peerConnection.createOffer(null);
            await peerConnection.setLocalDescription(offerInit);

            logger.LogDebug($"Sending SDP offer to client {context.UserEndPoint}.");

            context.WebSocket.Send(offerInit.sdp);

            return peerConnection;
        }

        private static RTCPeerConnection CreatePeerConnection(WebSocketContext context)
        {
            RTCConfiguration pcConfiguration = new RTCConfiguration
            {
                certificates = new List<RTCCertificate>
                {
                    new RTCCertificate
                    {
                        X_CertificatePath = DTLS_CERTIFICATE_PATH,
                        X_KeyPath = DTLS_KEY_PATH,
                        X_Fingerprint = DTLS_CERTIFICATE_FINGERPRINT
                    }
                },
                X_RemoteSignallingAddress = context.UserEndPoint.Address,
                iceServers = new List<RTCIceServer> { new RTCIceServer { urls = SIPSORCERY_STUN_SERVER  } }
            };

            var peerConnection = new RTCPeerConnection(pcConfiguration);

#if DTLS_IS_ENABLED
            SIPSorceryMedia.DtlsHandshake dtls = new SIPSorceryMedia.DtlsHandshake(DTLS_CERTIFICATE_PATH, DTLS_KEY_PATH);
            //dtls.Debug = true;
#endif

            // Add inactive audio and video tracks.
            MediaStreamTrack audioTrack = new MediaStreamTrack(SDPMediaTypesEnum.audio, false, new List<SDPMediaFormat> { new SDPMediaFormat(SDPMediaFormatsEnum.PCMU) }, MediaStreamStatusEnum.RecvOnly);
            peerConnection.addTrack(audioTrack);
            MediaStreamTrack videoTrack = new MediaStreamTrack(SDPMediaTypesEnum.video, false, new List<SDPMediaFormat> { new SDPMediaFormat(SDPMediaFormatsEnum.VP8) }, MediaStreamStatusEnum.Inactive);
            peerConnection.addTrack(videoTrack);

            peerConnection.onicecandidate += (candidate) =>
            {
                logger.LogDebug($"ICE candidate discovered: {candidate}.");

                // Host candidates are included in the SDP we send.
                if (candidate.type != RTCIceCandidateType.host)
                {
                    context.WebSocket.Send($"candidate:{candidate}");
                }
            };

            // Peer ICE connection state changes are for ICE events such as the STUN checks completing.
            peerConnection.oniceconnectionstatechange += (state) =>
            {
                logger.LogDebug($"ICE connection state change to {state}.");

                if (state == RTCIceConnectionState.connected)
                {
                    var remoteEndPoint = peerConnection.IceSession.NominatedCandidate.DestinationEndPoint;
                    //var remoteEndPoint = peerConnection.AudioDestinationEndPoint;
                    logger.LogInformation($"ICE connected to remote end point {remoteEndPoint}.");

                    if (peerConnection.RemotePeerDtlsFingerprint == null)
                    {
                        logger.LogWarning("DTLS handshake cannot proceed, no fingerprint was available for the remote peer.");
                        peerConnection.Close("No DTLS fingerprint.");
                    }
                    else
                    {
#if DTLS_IS_ENABLED
                        if (peerConnection.IceRole == IceRolesEnum.active)
                        {
                            logger.LogDebug("Starting DLS handshake as client task.");
                            _ = Task.Run(() =>
                            {
                                bool handshakedResult = DoDtlsHandshake(peerConnection, dtls, true, peerConnection.RemotePeerDtlsFingerprint);
                                logger.LogDebug($"DTLS handshake result {handshakedResult}.");
                            });
                        }
                        else
                        {
                            logger.LogDebug("Starting DLS handshake as server task.");
                            _ = Task.Run(() =>
                            {
                                bool handshakedResult = DoDtlsHandshake(peerConnection, dtls, false, peerConnection.RemotePeerDtlsFingerprint);
                                logger.LogDebug($"DTLS handshake result {handshakedResult}.");
                            });
                        }
#endif
                    }
                }
            };

            // Peer connection state changes are for DTLS handshake completing.
            peerConnection.onconnectionstatechange += (state) =>
            {
                logger.LogDebug($"Peer connection state change to {state}.");
            };

            peerConnection.OnReceiveReport += (type, rtcp) => logger.LogDebug($"RTCP {type} report received.");
            peerConnection.OnRtcpBye += (reason) => logger.LogDebug($"RTCP BYE receive, reason: {reason}.");

            peerConnection.IceSession.StartGathering();

            return peerConnection;
        }

        private static async Task WebSocketMessageReceived(WebSocketContext context, RTCPeerConnection peerConnection, string message)
        {
            try
            {
                if (peerConnection.localDescription == null)
                {
                    //logger.LogDebug("Offer SDP: " + message);
                    logger.LogDebug("Offer SDP received.");

                    // Add local media tracks depending on what was offered. Also add local tracks with the same media ID as 
                    // the remote tracks so that the media announcement in the SDP answer are in the same order.
                    SDP remoteSdp = SDP.ParseSDPDescription(message);
                    peerConnection.setRemoteDescription(new RTCSessionDescriptionInit { sdp = message, type = RTCSdpType.offer });

                    var answer = peerConnection.createAnswer(null);
                    await peerConnection.setLocalDescription(answer);

                    context.WebSocket.Send(answer.sdp);
                }
                else if (peerConnection.remoteDescription == null)
                {
                    logger.LogDebug("Answer SDP: " + message);
                    peerConnection.setRemoteDescription(new RTCSessionDescriptionInit { sdp = message, type = RTCSdpType.answer });
                }
                else
                {
                    logger.LogDebug("ICE Candidate: " + message);

                    if (string.IsNullOrWhiteSpace(message) || message.Trim().ToLower() == SDP.END_ICE_CANDIDATES_ATTRIBUTE)
                    {
                        logger.LogDebug("End of candidates message received.");
                    }
                    else
                    {
                        peerConnection.addIceCandidate(new RTCIceCandidateInit { candidate = message });
                    }
                }
            }
            catch (Exception excp)
            {
                logger.LogError("Exception WebSocketMessageReceived. " + excp.Message);
            }
        }

        /// <summary>
        ///  Adds a console logger. Can be omitted if internal SIPSorcery debug and warning messages are not required.
        /// </summary>
        private static void AddConsoleLogger()
        {
            var loggerFactory = new Microsoft.Extensions.Logging.LoggerFactory();
            var loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
                .WriteTo.Console()
                .CreateLogger();
            loggerFactory.AddSerilog(loggerConfig);
            SIPSorcery.Sys.Log.LoggerFactory = loggerFactory;
        }

#if DTLS_IS_ENABLED

        /// <summary>
        /// Hands the socket handle to the DTLS context and waits for the handshake to complete.
        /// </summary>
        /// <param name="webRtcSession">The WebRTC session to perform the DTLS handshake on.</param>
        private static bool DoDtlsHandshake(RTCPeerConnection peerConnection, SIPSorceryMedia.DtlsHandshake dtls, bool isClient, byte[] sdpFingerprint)
        {
            logger.LogDebug("DoDtlsHandshake started.");

            if (!File.Exists(DTLS_CERTIFICATE_PATH))
            {
                throw new ApplicationException($"The DTLS certificate file could not be found at {DTLS_CERTIFICATE_PATH}.");
            }
            else if (!File.Exists(DTLS_KEY_PATH))
            {
                throw new ApplicationException($"The DTLS key file could not be found at {DTLS_KEY_PATH}.");
            }

            int res = 0;
            bool fingerprintMatch = false;

            if (isClient)
            {
                IPEndPoint peerEP = peerConnection.IceSession.NominatedCandidate.DestinationEndPoint;
                logger.LogDebug($"DTLS client handshake starting to {peerEP}.");

                // For the DTLS handshake to work connect must be called on the socket so openssl knows where to send.
                var rtpSocket = peerConnection.GetRtpChannel(SDPMediaTypesEnum.audio).RtpSocket;
                rtpSocket.Connect(peerEP);

                byte[] fingerprint = null;
                res = dtls.DoHandshakeAsClient((ulong)rtpSocket.Handle, (short)peerEP.AddressFamily.GetHashCode(), peerEP.Address.GetAddressBytes(), (ushort)peerEP.Port, ref fingerprint);
                if (fingerprint != null)
                {
                    logger.LogDebug($"DTLS server fingerprint {ByteBufferInfo.HexStr(fingerprint)}.");
                    fingerprintMatch = sdpFingerprint.SequenceEqual(fingerprint);
                }
            }
            else
            {
                byte[] fingerprint = null;
                res = dtls.DoHandshakeAsServer((ulong)peerConnection.GetRtpChannel(SDPMediaTypesEnum.audio).RtpSocket.Handle, ref fingerprint);
                if (fingerprint != null)
                {
                    logger.LogDebug($"DTLS client fingerprint {ByteBufferInfo.HexStr(fingerprint)}.");
                    fingerprintMatch = sdpFingerprint.SequenceEqual(fingerprint);
                }
            }

            logger.LogDebug("DtlsContext initialisation result=" + res);

            if (dtls.IsHandshakeComplete())
            {
                logger.LogDebug("DTLS negotiation complete.");

                if (!fingerprintMatch)
                {
                    logger.LogWarning("DTLS fingerprint mismatch.");
                    return false;
                }
                else
                {
                    var srtpSendContext = new SIPSorceryMedia.Srtp(dtls, isClient);
                    var srtpReceiveContext = new SIPSorceryMedia.Srtp(dtls, !isClient);

                    peerConnection.SetSecurityContext(
                        srtpSendContext.ProtectRTP,
                        srtpReceiveContext.UnprotectRTP,
                        srtpSendContext.ProtectRTCP,
                        srtpReceiveContext.UnprotectRTCP);

                    return true;
                }
            }
            else
            {
                return false;
            }
        }
#endif
    }
}
