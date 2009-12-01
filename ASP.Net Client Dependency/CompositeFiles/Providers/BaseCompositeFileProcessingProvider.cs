using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ClientDependency.Core.Config;
using System.IO;
using System.Web;
using System.Net;
using System.IO.Compression;
using System.Configuration.Provider;

namespace ClientDependency.Core.CompositeFiles.Providers
{
	public abstract class BaseCompositeFileProcessingProvider : ProviderBase
	{

		#region Provider Members

		public abstract FileInfo SaveCompositeFile(byte[] fileContents, ClientDependencyType type);
		public abstract byte[] CombineFiles(string[] strFiles, HttpContext context, ClientDependencyType type, out List<CompositeFileDefinition> fileDefs);
		public abstract byte[] CompressBytes(CompressionType type, byte[] fileBytes);

        public virtual bool EnableCssMinify { get; set; }
        public virtual bool EnableJsMinify { get; set; }

		#endregion

        public override void Initialize(string name, System.Collections.Specialized.NameValueCollection config)
        {
            EnableCssMinify = true;//enabled by default
            EnableJsMinify = true;//enabled by default
            if (config != null)
            {
                if (config["enableCssMinify"] != null)
                {
                    bool enableCssMinify;
                    if (bool.TryParse(config["enableCssMinify"], out enableCssMinify))
                        EnableCssMinify = enableCssMinify;
                }
                if (config["enableJsMinify"] != null)
                {
                    bool enableJsMinify;
                    if (bool.TryParse(config["enableJsMinify"], out enableJsMinify))
                        EnableJsMinify = enableJsMinify;
                }
            }

            base.Initialize(name, config);
        }

        protected string MinifyFile(string fileContents, ClientDependencyType type)
        {
            switch (type)
            {
                case ClientDependencyType.Css:
                    return EnableCssMinify ? CssMin.CompressCSS(fileContents) : fileContents;
                case ClientDependencyType.Javascript:
                    return EnableJsMinify ? JSMin.CompressJS(fileContents) : fileContents;
                default:
                    return fileContents;
            }
        }

		/// <summary>
		/// This ensures that all paths (i.e. images) in a CSS file have their paths change to absolute paths.
		/// </summary>
		/// <param name="fileContents"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		protected string ParseCssFilePaths(string fileContents, ClientDependencyType type, string url)
		{
			//if it is a CSS file we need to parse the URLs
			if (type == ClientDependencyType.Css)
			{

				fileContents = CssFileUrlFormatter.TransformCssFile(fileContents, MakeUri(url));
			}
			return fileContents;
		}

		/// <summary>
		/// Checks if the url is a local/relative uri, if it is, it makes it absolute based on the 
		/// current request uri.
		/// </summary>
		/// <param name="url"></param>
		/// <returns></returns>
		protected Uri MakeUri(string url)
		{
			Uri uri = new Uri(url, UriKind.RelativeOrAbsolute);
			if (!uri.IsAbsoluteUri)
			{
				string http = HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority);
				Uri absoluteUrl = new Uri(new Uri(http), uri);
				return absoluteUrl;
			}
			return uri;
		}

		/// <summary>
		/// Tries to convert the url to a uri, then read the request into a string and return it.
		/// This takes into account relative vs absolute URI's
		/// </summary>
		/// <param name="url"></param>
		/// <param name="requestContents"></param>
		/// <returns>true if successful, false if not successful</returns>
		/// <remarks>
		/// if the path is a relative local path, the we use Server.Execute to get the request output, otherwise
		/// if it is an absolute path, a WebClient request is made to fetch the contents.
		/// </remarks>
		protected bool TryReadUri(string url, out string requestContents)
		{
			Uri uri;
			if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uri))
			{
				if (uri.IsAbsoluteUri)
				{
					WebClient client = new WebClient();
					try
					{
						requestContents = client.DownloadString(uri.AbsoluteUri);
						return true;
					}
					catch (Exception ex)
					{
						HttpContext.Current.Trace.Warn("ClientDependency", "Could not load file contents from " + url, ex);
						//System.Diagnostics.Debug.Assert(false, "Could not load file contents from " + url, ex.Message);
					}
				}
				else
				{
					//its a relative path so use the execute method
					StringWriter sw = new StringWriter();
					try
					{
						HttpContext.Current.Server.Execute(url, sw);
						requestContents = sw.ToString();
						sw.Close();
						return true;
					}
					catch (Exception ex)
					{
						HttpContext.Current.Trace.Warn("ClientDependency", "Could not load file contents from " + url, ex);
						//System.Diagnostics.Debug.Assert(false, "Could not load file contents from " + url, ex.Message);
					}
				}

			}
			requestContents = "";
			return false;
		}
	}
}