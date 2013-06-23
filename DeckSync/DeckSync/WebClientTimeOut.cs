using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace DeckSync
{
	public class WebClientTimeOut : WebClient
	{
		protected override WebRequest GetWebRequest(Uri address)
		{
			WebRequest webRequest = base.GetWebRequest(address);
			webRequest.Timeout = TimeOut;
			return webRequest;
		}
		public int TimeOut
		{
			set;
			get;
		}
	}
}
