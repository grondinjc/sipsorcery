﻿//-----------------------------------------------------------------------------
// Filename: RTCPeerConnectionUnitTest.cs
//
// Description: Unit tests for the RTCPeerConnection class.
//
// History:
// 16 Mar 2020	Aaron Clauson	Created.
// 14 Dec 2020  Aaron Clauson   Moved from unit to integration tests (while not 
//              really integration tests the duration is long'ish for a unit test).
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions;
using Xunit;

namespace SIPSorcery.Net.IntegrationTests
{
    [Trait("Category", "integration")]
    public class RTCPeerConnectionUnitTest
    {
        private Microsoft.Extensions.Logging.ILogger logger = null;

        public RTCPeerConnectionUnitTest(Xunit.Abstractions.ITestOutputHelper output)
        {
            logger = SIPSorcery.UnitTests.TestLogHelper.InitTestLogger(output);
        }

        /// <summary>
        /// Tests that generating the local SDP offer works correctly.
        /// </summary>
        /// <code>
        /// // Javascript equivalent:
        /// let pc = new RTCPeerConnection(null);
        /// const offer = await pc.createOffer();
        /// console.log(offer);
        /// </code>
        [Fact]
        public void GenerateLocalOfferUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            RTCPeerConnection pc = new RTCPeerConnection(null);
            var offer = pc.createOffer(new RTCOfferOptions());

            Assert.NotNull(offer);

            logger.LogDebug(offer.ToString());
        }

        /// <summary>
        /// Tests that generating the local SDP offer with an audio track works correctly.
        /// </summary>
        /// <code>
        /// // Javascript equivalent:
        /// const constraints = {'audio': true }
        /// const localStream = await navigator.mediaDevices.getUserMedia({video: false, audio: true});
        /// let pc = new RTCPeerConnection(null);
        /// pc.addTrack(localStream.getTracks()[0]);
        /// const offer = await pc.createOffer();
        /// console.log(offer);
        /// </code>
        [Fact]
        public void GenerateLocalOfferWithAudioTrackUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            RTCPeerConnection pc = new RTCPeerConnection(null);
            var audioTrack = new MediaStreamTrack(SDPMediaTypesEnum.audio, false, new List<SDPAudioVideoMediaFormat> { new SDPAudioVideoMediaFormat(SDPWellKnownMediaFormatsEnum.PCMU) });
            pc.addTrack(audioTrack);
            var offer = pc.createOffer(new RTCOfferOptions());

            SDP offerSDP = SDP.ParseSDPDescription(offer.sdp);

            Assert.NotNull(offer);
            Assert.NotNull(offer.sdp);
            Assert.Equal(RTCSdpType.offer, offer.type);
            Assert.Single(offerSDP.Media);
            Assert.Contains(offerSDP.Media, x => x.Media == SDPMediaTypesEnum.audio);

            logger.LogDebug(offer.sdp);
        }

        /// <summary>
        /// Tests that attempting to send an RTCP feedback report for an audio stream works correctly.
        /// </summary>
        [Fact]
        public void SendVideoRtcpFeedbackReportUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            RTCConfiguration pcConfiguration = new RTCConfiguration
            {
                X_UseRtpFeedbackProfile = true
            };

            RTCPeerConnection pcSrc = new RTCPeerConnection(pcConfiguration);
            var videoTrackSrc = new MediaStreamTrack(SDPMediaTypesEnum.video, false, new List<SDPAudioVideoMediaFormat> { new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.video, 96, "VP8", 90000) });
            pcSrc.addTrack(videoTrackSrc);
            var offer = pcSrc.createOffer(new RTCOfferOptions());

            logger.LogDebug($"offer: {offer.sdp}");

            RTCPeerConnection pcDst = new RTCPeerConnection(pcConfiguration);
            var videoTrackDst = new MediaStreamTrack(SDPMediaTypesEnum.video, false, new List<SDPAudioVideoMediaFormat> { new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.video, 96, "VP8", 90000) });
            pcDst.addTrack(videoTrackDst);

            var setOfferResult = pcDst.setRemoteDescription(offer);
            Assert.Equal(SetDescriptionResultEnum.OK, setOfferResult);

            var answer = pcDst.createAnswer(null);
            var setAnswerResult = pcSrc.setRemoteDescription(answer);
            Assert.Equal(SetDescriptionResultEnum.OK, setAnswerResult);

            logger.LogDebug($"answer: {answer.sdp}");

            RTCPFeedback pliReport = new RTCPFeedback(pcDst.VideoLocalTrack.Ssrc, pcDst.VideoRemoteTrack.Ssrc, PSFBFeedbackTypesEnum.PLI);
            pcDst.SendRtcpFeedback(SDPMediaTypesEnum.video, pliReport);
        }

        /// <summary>
        /// Checks that the media formats are correctly negotiated when for a remote offer and the local
        /// </summary>
        [Fact]
        public void CheckMediaFormatNegotiationUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            // By default offers made by us always put audio first. Create a remote SDP offer 
            // with the video first.
            string remoteSdp =
            @"v=0
o=- 62533 0 IN IP4 127.0.0.1
s=-
t=0 0
a=group:BUNDLE 0 1
a=msid-semantic: WMS
m=audio 57148 UDP/TLS/RTP/SAVP 0 101
c=IN IP6 2a02:8084:6981:7880::76
a=rtcp:9 IN IP4 0.0.0.0
a=candidate:2944 1 udp 659136 192.168.11.50 57148 typ host generation 0
a=candidate:2488 1 udp 659136 192.168.0.50 57148 typ host generation 0
a=candidate:2507 1 udp 659136 fe80::54a9:d238:b2ee:ceb%24 57148 typ host generation 0
a=candidate:3159 1 udp 659136 2a02:8084:6981:7880::76 57148 typ host generation 0
a=ice-ufrag:CUTK
a=ice-pwd:QTCZWDIEBCIBGOYAGSIXRFIL
a=ice-options:ice2,trickle
a=fingerprint:sha-256 06:2F:61:85:1F:83:64:88:1B:93:93:8C:E5:FF:1C:D9:82:EA:60:97:1E:0D:DA:FA:28:11:00:FA:74:69:23:DB
a=setup:actpass
a=mid:0
a=sendrecv
a=rtcp-mux
a=rtpmap:0 PCMU/8000
a=rtpmap:101 telephone-event/8000
a=fmtp:101 0-16
m=video 9 UDP/TLS/RTP/SAVP 100
c=IN IP4 0.0.0.0
a=rtcp:9 IN IP4 0.0.0.0
a=ice-ufrag:CUTK
a=ice-pwd:QTCZWDIEBCIBGOYAGSIXRFIL
a=ice-options:ice2,trickle
a=fingerprint:sha-256 06:2F:61:85:1F:83:64:88:1B:93:93:8C:E5:FF:1C:D9:82:EA:60:97:1E:0D:DA:FA:28:11:00:FA:74:69:23:DB
a=setup:actpass
a=mid:1
a=sendrecv
a=rtcp-mux
a=rtpmap:100 VP8/90000";

            // Create a local session and add the video track first.
            RTCPeerConnection pc = new RTCPeerConnection(null);
            MediaStreamTrack localAudioTrack = new MediaStreamTrack(SDPMediaTypesEnum.audio, false, new List<SDPAudioVideoMediaFormat> {
                new SDPAudioVideoMediaFormat(SDPWellKnownMediaFormatsEnum.PCMU),
                new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.audio, 110, "OPUS/48000/2")
            });
            pc.addTrack(localAudioTrack);
            MediaStreamTrack localVideoTrack = new MediaStreamTrack(SDPMediaTypesEnum.video, false, new List<SDPAudioVideoMediaFormat> { new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.video, 96, "VP8", 90000) });
            pc.addTrack(localVideoTrack);

            var offer = SDP.ParseSDPDescription(remoteSdp);

            logger.LogDebug($"Remote offer: {offer}");

            var result = pc.SetRemoteDescription(SIP.App.SdpType.offer, offer);

            logger.LogDebug($"Set remote description on local session result {result}.");

            Assert.Equal(SetDescriptionResultEnum.OK, result);

            var answer = pc.CreateAnswer(null);

            logger.LogDebug($"Local answer: {answer}");

            Assert.Equal(2, pc.AudioLocalTrack.Capabilities.Count());
            Assert.Equal(0, pc.AudioLocalTrack.Capabilities.Single(x => x.Name() == "PCMU").ID);
            Assert.Equal(100, pc.VideoLocalTrack.Capabilities.Single(x => x.Name() == "VP8").ID);

            pc.Close("normal");
        }

        /// <summary>
        /// Checks that the media identifier tags are correctly reused in the generated answer
        /// tracks.
        /// </summary>
        [Fact]
        public void CheckAudioVideoMediaIdentifierTagsAreReusedForAnswerUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            // In this SDP, the audio media identifier's tag is "bar" and the video media identifier's tag is "foo"
            string remoteSdp =
            @"v=0
o=- 1064364449942365659 2 IN IP4 127.0.0.1
s=-
t=0 0
a=group:BUNDLE bar foo
a=msid-semantic: WMS stream0
m=audio 9 UDP/TLS/RTP/SAVPF 111 103 104 9 102 0 8 106 105 13 110 112 113 126
c=IN IP4 0.0.0.0
a=rtcp:9 IN IP4 0.0.0.0
a=ice-ufrag:G5P/
a=ice-pwd:FICf2eBzvl5r/O/uf1ktSyuc
a=ice-options:trickle renomination
a=fingerprint:sha-256 5D:03:7C:22:69:2E:E7:10:17:5F:31:86:E6:47:2F:6F:1D:4C:A6:BF:5B:DE:0C:FB:8A:17:15:AA:22:63:0C:FD
a=setup:actpass
a=mid:bar
a=extmap:1 urn:ietf:params:rtp-hdrext:ssrc-audio-level
a=extmap:2 http://www.webrtc.org/experiments/rtp-hdrext/abs-send-time
a=extmap:3 http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01
a=sendrecv
a=rtcp-mux
a=rtpmap:111 opus/48000/2
a=rtcp-fb:111 transport-cc
a=fmtp:111 minptime=10;useinbandfec=1
a=rtpmap:103 ISAC/16000
a=rtpmap:104 ISAC/32000
a=rtpmap:9 G722/8000
a=rtpmap:102 ILBC/8000
a=rtpmap:0 PCMU/8000
a=rtpmap:8 PCMA/8000
a=rtpmap:106 CN/32000
a=rtpmap:105 CN/16000
a=rtpmap:13 CN/8000
a=rtpmap:110 telephone-event/48000
a=rtpmap:112 telephone-event/32000
a=rtpmap:113 telephone-event/16000
a=rtpmap:126 telephone-event/8000
a=ssrc:3780525913 cname:FLLo3gHcblO+MbrR
a=ssrc:3780525913 msid:stream0 audio0
a=ssrc:3780525913 mslabel:stream0
a=ssrc:3780525913 label:audio0
m=video 9 UDP/TLS/RTP/SAVPF 96 97 98 99 100 101 127
c=IN IP4 0.0.0.0
a=rtcp:9 IN IP4 0.0.0.0
a=ice-ufrag:G5P/
a=ice-pwd:FICf2eBzvl5r/O/uf1ktSyuc
a=ice-options:trickle renomination
a=fingerprint:sha-256 5D:03:7C:22:69:2E:E7:10:17:5F:31:86:E6:47:2F:6F:1D:4C:A6:BF:5B:DE:0C:FB:8A:17:15:AA:22:63:0C:FD
a=setup:actpass
a=mid:foo
a=extmap:14 urn:ietf:params:rtp-hdrext:toffset
a=extmap:2 http://www.webrtc.org/experiments/rtp-hdrext/abs-send-time
a=extmap:13 urn:3gpp:video-orientation
a=extmap:3 http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01
a=extmap:5 http://www.webrtc.org/experiments/rtp-hdrext/playout-delay
a=extmap:6 http://www.webrtc.org/experiments/rtp-hdrext/video-content-type
a=extmap:7 http://www.webrtc.org/experiments/rtp-hdrext/video-timing
a=extmap:8 http://www.webrtc.org/experiments/rtp-hdrext/color-space
a=sendrecv
a=rtcp-mux
a=rtcp-rsize
a=rtpmap:96 VP8/90000
a=rtcp-fb:96 goog-remb
a=rtcp-fb:96 transport-cc
a=rtcp-fb:96 ccm fir
a=rtcp-fb:96 nack
a=rtcp-fb:96 nack pli
a=rtpmap:97 rtx/90000
a=fmtp:97 apt=96
a=rtpmap:98 VP9/90000
a=rtcp-fb:98 goog-remb
a=rtcp-fb:98 transport-cc
a=rtcp-fb:98 ccm fir
a=rtcp-fb:98 nack
a=rtcp-fb:98 nack pli
a=rtpmap:99 rtx/90000
a=fmtp:99 apt=98
a=rtpmap:100 red/90000
a=rtpmap:101 rtx/90000
a=fmtp:101 apt=100
a=rtpmap:127 ulpfec/90000
a=ssrc-group:FID 3851740345 4165955869
a=ssrc:3851740345 cname:FLLo3gHcblO+MbrR
a=ssrc:3851740345 msid:stream0 video0
a=ssrc:3851740345 mslabel:stream0
a=ssrc:3851740345 label:video0
a=ssrc:4165955869 cname:FLLo3gHcblO+MbrR
a=ssrc:4165955869 msid:stream0 video0
a=ssrc:4165955869 mslabel:stream0
a=ssrc:4165955869 label:video0";

            RTCPeerConnection pc = new RTCPeerConnection(null);
            var audioTrack = new MediaStreamTrack(SDPMediaTypesEnum.audio, false, new List<SDPAudioVideoMediaFormat> { new SDPAudioVideoMediaFormat(SDPWellKnownMediaFormatsEnum.PCMU) });
            pc.addTrack(audioTrack);
            MediaStreamTrack localVideoTrack = new MediaStreamTrack(SDPMediaTypesEnum.video, false, new List<SDPAudioVideoMediaFormat> { new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.video, 96, "VP8", 90000) });
            pc.addTrack(localVideoTrack);

            var offer = SDP.ParseSDPDescription(remoteSdp);

            logger.LogDebug($"Remote offer: {offer}");

            var result = pc.SetRemoteDescription(SIP.App.SdpType.offer, offer);

            logger.LogDebug($"Set remote description on local session result {result}.");

            Assert.Equal(SetDescriptionResultEnum.OK, result);

            var answer = pc.CreateAnswer(null);
            var answerString = answer.ToString();

            logger.LogDebug($"Local answer: {answer}");

            Assert.Equal("bar", answer.Media[0].MediaID);
            Assert.Equal(SDPMediaTypesEnum.audio, answer.Media[0].Media);
            Assert.Equal("foo", answer.Media[1].MediaID);
            Assert.Equal(SDPMediaTypesEnum.video, answer.Media[1].Media);
            Assert.Contains("a=group:BUNDLE bar foo", answerString);
            Assert.Contains("a=mid:bar", answerString);
            Assert.Contains("a=mid:foo", answerString);

            pc.Close("normal");
        }

        /// <summary>
        /// Checks that the media identifier tags for datachannel (application data) are correctly reused in
        /// the generated answer.
        /// </summary>
        [Fact]
        public void CheckDataChannelMediaIdentifierTagsAreReusedForAnswerUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            // In this SDP, the datachannel1's media identifier's tag is "application1"
            string remoteSdp =
            @"v=0
o=- 6803632431644503613 2 IN IP4 127.0.0.1
s=-
t=0 0
a=group:BUNDLE application1
a=extmap-allow-mixed
a=msid-semantic: WMS
m=application 9 UDP/DTLS/SCTP webrtc-datachannel
c=IN IP4 0.0.0.0
a=ice-ufrag:xort
a=ice-pwd:6/W7mcRWqCOpmKhfY4a+KK0m
a=ice-options:trickle
a=fingerprint:sha-256 B7:C9:01:0F:B4:BE:00:45:73:4B:F4:52:A9:E7:87:04:72:EB:1A:DC:30:AF:BD:5D:19:BF:12:DE:FF:AF:74:00
a=setup:actpass
a=mid:application1
a=sctp-port:5000
a=max-message-size:262144";

            RTCPeerConnection pc = new RTCPeerConnection(null);

            var offer = SDP.ParseSDPDescription(remoteSdp);

            logger.LogDebug($"Remote offer: {offer}");

            var result = pc.SetRemoteDescription(SIP.App.SdpType.offer, offer);

            logger.LogDebug($"Set remote description on local session result {result}.");

            Assert.Equal(SetDescriptionResultEnum.OK, result);

            var answer = pc.CreateAnswer(null);
            var answerString = answer.ToString();

            logger.LogDebug($"Local answer: {answer}");

            Assert.Equal("application1", answer.Media[0].MediaID);
            Assert.Equal(SDPMediaTypesEnum.application, answer.Media[0].Media);
            Assert.Contains("a=group:BUNDLE application1", answerString);
            Assert.Contains("a=mid:application1", answerString);

            pc.Close("normal");
        }

        /// <summary>
        /// Checks that the media identifier tags in the generated answer are in the same order as in the 
        /// received offer.
        /// </summary>
        [Fact]
        public void CheckMediaIdentifierTagOrderRemainsForAnswerUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            // In this SDP, the audio media identifier's tag is "zzz" and the video media identifier's tag is "aaa".
            // Such tag are meant to ensure that we do not sort sdp's media tracks by alphabetical order.
            string remoteSdp =
            @"v=0
o=- 1064364449942365659 2 IN IP4 127.0.0.1
s=-
t=0 0
a=group:BUNDLE zzz aaa 
a=msid-semantic: WMS stream0
m=audio 9 UDP/TLS/RTP/SAVPF 111 103 104 9 102 0 8 106 105 13 110 112 113 126
c=IN IP4 0.0.0.0
a=rtcp:9 IN IP4 0.0.0.0
a=ice-ufrag:G5P/
a=ice-pwd:FICf2eBzvl5r/O/uf1ktSyuc
a=ice-options:trickle renomination
a=fingerprint:sha-256 5D:03:7C:22:69:2E:E7:10:17:5F:31:86:E6:47:2F:6F:1D:4C:A6:BF:5B:DE:0C:FB:8A:17:15:AA:22:63:0C:FD
a=setup:actpass
a=mid:zzz
a=extmap:1 urn:ietf:params:rtp-hdrext:ssrc-audio-level
a=extmap:2 http://www.webrtc.org/experiments/rtp-hdrext/abs-send-time
a=extmap:3 http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01
a=sendrecv
a=rtcp-mux
a=rtpmap:111 opus/48000/2
a=rtcp-fb:111 transport-cc
a=fmtp:111 minptime=10;useinbandfec=1
a=rtpmap:103 ISAC/16000
a=rtpmap:104 ISAC/32000
a=rtpmap:9 G722/8000
a=rtpmap:102 ILBC/8000
a=rtpmap:0 PCMU/8000
a=rtpmap:8 PCMA/8000
a=rtpmap:106 CN/32000
a=rtpmap:105 CN/16000
a=rtpmap:13 CN/8000
a=rtpmap:110 telephone-event/48000
a=rtpmap:112 telephone-event/32000
a=rtpmap:113 telephone-event/16000
a=rtpmap:126 telephone-event/8000
a=ssrc:3780525913 cname:FLLo3gHcblO+MbrR
a=ssrc:3780525913 msid:stream0 audio0
a=ssrc:3780525913 mslabel:stream0
a=ssrc:3780525913 label:audio0
m=video 9 UDP/TLS/RTP/SAVPF 96 97 98 99 100 101 127
c=IN IP4 0.0.0.0
a=rtcp:9 IN IP4 0.0.0.0
a=ice-ufrag:G5P/
a=ice-pwd:FICf2eBzvl5r/O/uf1ktSyuc
a=ice-options:trickle renomination
a=fingerprint:sha-256 5D:03:7C:22:69:2E:E7:10:17:5F:31:86:E6:47:2F:6F:1D:4C:A6:BF:5B:DE:0C:FB:8A:17:15:AA:22:63:0C:FD
a=setup:actpass
a=mid:aaa
a=extmap:14 urn:ietf:params:rtp-hdrext:toffset
a=extmap:2 http://www.webrtc.org/experiments/rtp-hdrext/abs-send-time
a=extmap:13 urn:3gpp:video-orientation
a=extmap:3 http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01
a=extmap:5 http://www.webrtc.org/experiments/rtp-hdrext/playout-delay
a=extmap:6 http://www.webrtc.org/experiments/rtp-hdrext/video-content-type
a=extmap:7 http://www.webrtc.org/experiments/rtp-hdrext/video-timing
a=extmap:8 http://www.webrtc.org/experiments/rtp-hdrext/color-space
a=sendrecv
a=rtcp-mux
a=rtcp-rsize
a=rtpmap:96 VP8/90000
a=rtcp-fb:96 goog-remb
a=rtcp-fb:96 transport-cc
a=rtcp-fb:96 ccm fir
a=rtcp-fb:96 nack
a=rtcp-fb:96 nack pli
a=rtpmap:97 rtx/90000
a=fmtp:97 apt=96
a=rtpmap:98 VP9/90000
a=rtcp-fb:98 goog-remb
a=rtcp-fb:98 transport-cc
a=rtcp-fb:98 ccm fir
a=rtcp-fb:98 nack
a=rtcp-fb:98 nack pli
a=rtpmap:99 rtx/90000
a=fmtp:99 apt=98
a=rtpmap:100 red/90000
a=rtpmap:101 rtx/90000
a=fmtp:101 apt=100
a=rtpmap:127 ulpfec/90000
a=ssrc-group:FID 3851740345 4165955869
a=ssrc:3851740345 cname:FLLo3gHcblO+MbrR
a=ssrc:3851740345 msid:stream0 video0
a=ssrc:3851740345 mslabel:stream0
a=ssrc:3851740345 label:video0
a=ssrc:4165955869 cname:FLLo3gHcblO+MbrR
a=ssrc:4165955869 msid:stream0 video0
a=ssrc:4165955869 mslabel:stream0
a=ssrc:4165955869 label:video0";

            RTCPeerConnection pc = new RTCPeerConnection(null);
            var audioTrack = new MediaStreamTrack(SDPMediaTypesEnum.audio, false, new List<SDPAudioVideoMediaFormat> { new SDPAudioVideoMediaFormat(SDPWellKnownMediaFormatsEnum.PCMU) });
            pc.addTrack(audioTrack);
            MediaStreamTrack localVideoTrack = new MediaStreamTrack(SDPMediaTypesEnum.video, false, new List<SDPAudioVideoMediaFormat> { new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.video, 96, "VP8", 90000) });
            pc.addTrack(localVideoTrack);

            var offer = SDP.ParseSDPDescription(remoteSdp);

            logger.LogDebug($"Remote offer: {offer}");

            var result = pc.SetRemoteDescription(SIP.App.SdpType.offer, offer);

            logger.LogDebug($"Set remote description on local session result {result}.");

            Assert.Equal(SetDescriptionResultEnum.OK, result);

            var answer = pc.CreateAnswer(null);
            var answerString = answer.ToString();

            logger.LogDebug($"Local answer: {answer}");

            Assert.Equal("zzz", answer.Media[0].MediaID);
            Assert.Equal(SDPMediaTypesEnum.audio, answer.Media[0].Media);
            Assert.Equal("aaa", answer.Media[1].MediaID);
            Assert.Equal(SDPMediaTypesEnum.video, answer.Media[1].Media);
            Assert.Contains("a=group:BUNDLE zzz aaa", answerString);
            Assert.Contains("a=mid:zzz", answerString);
            Assert.Contains("a=mid:aaa", answerString);

            pc.Close("normal");
        }

        /// <summary>
        /// Checks that an inactive audio track gets added if the offer contains audio and video but
        /// the local peer connection only supports video.
        /// </summary>
        [Fact]
        public void CheckNoAudioNegotiationUnitTest()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            // By default offers made by us always put audio first. Create a remote SDP offer 
            // with the video first.
            string remoteSdp =
            @"v=0
o=- 62533 0 IN IP4 127.0.0.1
s=-
t=0 0
a=group:BUNDLE 0 1
a=msid-semantic: WMS
m=audio 57148 UDP/TLS/RTP/SAVP 0 101
c=IN IP6 2a02:8084:6981:7880::76
a=rtcp:9 IN IP4 0.0.0.0
a=candidate:2944 1 udp 659136 192.168.11.50 57148 typ host generation 0
a=candidate:2488 1 udp 659136 192.168.0.50 57148 typ host generation 0
a=candidate:2507 1 udp 659136 fe80::54a9:d238:b2ee:ceb%24 57148 typ host generation 0
a=candidate:3159 1 udp 659136 2a02:8084:6981:7880::76 57148 typ host generation 0
a=ice-ufrag:CUTK
a=ice-pwd:QTCZWDIEBCIBGOYAGSIXRFIL
a=ice-options:ice2,trickle
a=fingerprint:sha-256 06:2F:61:85:1F:83:64:88:1B:93:93:8C:E5:FF:1C:D9:82:EA:60:97:1E:0D:DA:FA:28:11:00:FA:74:69:23:DB
a=setup:actpass
a=mid:0
a=sendrecv
a=rtcp-mux
a=rtpmap:0 PCMU/8000
a=rtpmap:101 telephone-event/8000
a=fmtp:101 0-16
m=video 9 UDP/TLS/RTP/SAVP 100
c=IN IP4 0.0.0.0
a=rtcp:9 IN IP4 0.0.0.0
a=ice-ufrag:CUTK
a=ice-pwd:QTCZWDIEBCIBGOYAGSIXRFIL
a=ice-options:ice2,trickle
a=fingerprint:sha-256 06:2F:61:85:1F:83:64:88:1B:93:93:8C:E5:FF:1C:D9:82:EA:60:97:1E:0D:DA:FA:28:11:00:FA:74:69:23:DB
a=setup:actpass
a=mid:1
a=sendrecv
a=rtcp-mux
a=rtpmap:100 VP8/90000";

            // Create a local session and add the video track first.
            RTCPeerConnection pc = new RTCPeerConnection(null);
            MediaStreamTrack localVideoTrack = new MediaStreamTrack(SDPMediaTypesEnum.video, false, new List<SDPAudioVideoMediaFormat> { new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.video, 96, "VP8", 90000) });
            pc.addTrack(localVideoTrack);

            var offer = SDP.ParseSDPDescription(remoteSdp);

            logger.LogDebug($"Remote offer: {offer}");

            var result = pc.SetRemoteDescription(SIP.App.SdpType.offer, offer);

            logger.LogDebug($"Set remote description on local session result {result}.");

            Assert.Equal(SetDescriptionResultEnum.OK, result);

            var answer = pc.CreateAnswer(null);

            logger.LogDebug($"Local answer: {answer}");

            Assert.Equal(MediaStreamStatusEnum.Inactive, pc.AudioLocalTrack.StreamStatus);
            Assert.Equal(100, pc.VideoLocalTrack.Capabilities.Single(x => x.Name() == "VP8").ID);

            pc.Close("normal");
        }

        /// <summary>
        /// Tests that two peer connection instances can reach the connected state.
        /// </summary>
        [Fact]
        public async void CheckPeerConnectionEstablishment()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            var aliceConnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var bobConnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var alice = new RTCPeerConnection();
            alice.onconnectionstatechange += (state) =>
            {
                if (state == RTCPeerConnectionState.connected)
                {
                    logger.LogDebug("Alice connected.");
                    aliceConnected.SetResult(true);
                }
            };
            alice.addTrack(new MediaStreamTrack(SDPWellKnownMediaFormatsEnum.PCMU));
            var aliceOffer = alice.createOffer();
            await alice.setLocalDescription(aliceOffer);

            logger.LogDebug($"alice offer: {aliceOffer.sdp}");

            var bob = new RTCPeerConnection();
            bob.onconnectionstatechange += (state) =>
            {
                if (state == RTCPeerConnectionState.connected)
                {
                    logger.LogDebug("Bob connected.");
                    bobConnected.SetResult(true);
                }
            };
            bob.addTrack(new MediaStreamTrack(SDPWellKnownMediaFormatsEnum.PCMU));

            var setOfferResult = bob.setRemoteDescription(aliceOffer);
            Assert.Equal(SetDescriptionResultEnum.OK, setOfferResult);

            var bobAnswer = bob.createAnswer();
            await bob.setLocalDescription(bobAnswer);
            var setAnswerResult = alice.setRemoteDescription(bobAnswer);
            Assert.Equal(SetDescriptionResultEnum.OK, setAnswerResult);

            logger.LogDebug($"answer: {bobAnswer.sdp}");

            await Task.WhenAny(Task.WhenAll(aliceConnected.Task, bobConnected.Task), Task.Delay(2000));

            Assert.True(aliceConnected.Task.IsCompleted);
            Assert.True(aliceConnected.Task.Result);
            Assert.True(bobConnected.Task.IsCompleted);
            Assert.True(bobConnected.Task.Result);

            bob.close();
            alice.close();
        }

        /// <summary>
        /// Tests that two peer connection instances can establish a data channel.
        /// </summary>
        [Fact]
        public async void CheckDataChannelEstablishment()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            var aliceDataConnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var bobDataOpened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var alice = new RTCPeerConnection();
            var dc = await alice.createDataChannel("dc1", null);
            dc.onopen += () => aliceDataConnected.TrySetResult(true);
            var aliceOffer = alice.createOffer();
            await alice.setLocalDescription(aliceOffer);

            logger.LogDebug($"alice offer: {aliceOffer.sdp}");

            var bob = new RTCPeerConnection();
            RTCDataChannel bobData = null;
            bob.ondatachannel += (chan) =>
            {
                bobData = chan;
                bobDataOpened.TrySetResult(true);
            };

            var setOfferResult = bob.setRemoteDescription(aliceOffer);
            Assert.Equal(SetDescriptionResultEnum.OK, setOfferResult);

            var bobAnswer = bob.createAnswer();
            await bob.setLocalDescription(bobAnswer);
            var setAnswerResult = alice.setRemoteDescription(bobAnswer);
            Assert.Equal(SetDescriptionResultEnum.OK, setAnswerResult);

            logger.LogDebug($"answer: {bobAnswer.sdp}");

            await Task.WhenAny(Task.WhenAll(aliceDataConnected.Task, bobDataOpened.Task), Task.Delay(2000));

            Assert.True(aliceDataConnected.Task.IsCompleted);
            Assert.True(aliceDataConnected.Task.Result);
            Assert.True(bobDataOpened.Task.IsCompleted);
            Assert.True(bobDataOpened.Task.Result);
            Assert.True(dc.IsOpened);
            Assert.True(bobData.IsOpened);

            bob.close();
            alice.close();
        }
    }
}
