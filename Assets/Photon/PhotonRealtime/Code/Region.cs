// ----------------------------------------------------------------------------
// <copyright file="Region.cs" company="Exit Games GmbH">
//   Loadbalancing Framework for Photon - Copyright (C) 2018 Exit Games GmbH
// </copyright>
// <summary>
//   Represents regions in the Photon Cloud.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

#if UNITY_4_7 || UNITY_5 || UNITY_5_3_OR_NEWER
#define SUPPORTED_UNITY
#endif


namespace Photon.Realtime
{
    #if SUPPORTED_UNITY || NETFX_CORE
    #endif


    public class Region
    {
        public string Code { get; private set; }

        /// <summary>Unlike the CloudRegionCode, this may contain cluster information.</summary>
        public string Cluster { get; private set; }

        public string HostAndPort { get; protected internal set; }

        /// <summary>Weighted ping time.</summary>
        /// <remarks>
        /// Regions gets pinged 5 times (RegionPinger.Attempts).
        /// Out of those, the worst rtt is discarded and the best will be counted two times for a weighted average.
        /// </remarks>
        public int Ping { get; set; }

        public bool WasPinged { get { return this.Ping != int.MaxValue; } }

        public Region(string code, string address)
        {
            this.SetCodeAndCluster(code);
            this.HostAndPort = address;
            this.Ping = int.MaxValue;
        }

        public Region(string code, int ping)
        {
            this.SetCodeAndCluster(code);
            this.Ping = ping;
        }

        private void SetCodeAndCluster(string codeAsString)
        {
            if (codeAsString == null)
            {
                this.Code = "";
                this.Cluster = "";
                return;
            }

            codeAsString = codeAsString.ToLower();
            var slash = codeAsString.IndexOf('/');
            this.Code = slash <= 0 ? codeAsString : codeAsString.Substring(0, slash);
            this.Cluster = slash <= 0 ? "" : codeAsString.Substring(slash+1, codeAsString.Length-slash-1);
        }

        public override string ToString()
        {
            return this.ToString(false);
        }

        public string ToString(bool compact = false)
        {
            var regionCluster = this.Code;
            if (!string.IsNullOrEmpty(this.Cluster))
            {
                regionCluster += "/" + this.Cluster;
            }

            if (compact)
            {
                return string.Format("{0}:{1}", regionCluster, this.Ping);
            }
            else
            {
                return string.Format("{0}[{2}]: {1}ms", regionCluster, this.Ping, this.HostAndPort);
            }
        }
    }
}