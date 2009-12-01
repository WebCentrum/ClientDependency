﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Web.UI;
using System.Configuration.Provider;
using System.Web;
using System.Linq;
using ClientDependency.Core.Controls;
using ClientDependency.Core.Config;

namespace ClientDependency.Core.FileRegistration.Providers
{
	public abstract class BaseFileRegistrationProvider : ProviderBase
	{
		protected Control DependantControl { get; private set; }
		protected HashSet<IClientDependencyPath> FolderPaths { get; private set; }
        protected List<IClientDependencyFile> AllDependencies { get; private set; }

        /// <summary>
        /// Set to true to disable composite scripts so all scripts/css comes through as individual files.
        /// </summary>
        public bool IsDebugMode { get; set; }

		protected abstract void RegisterJsFiles(List<IClientDependencyFile> jsDependencies);
		protected abstract void RegisterCssFiles(List<IClientDependencyFile> cssDependencies);
		protected abstract void ProcessSingleJsFile(string js);
		protected abstract void ProcessSingleCssFile(string css);

        public override void Initialize(string name, System.Collections.Specialized.NameValueCollection config)
        {
            IsDebugMode = true;
            if (config != null && config["isDebug"] != null)
            {
                bool isDebug;
                if (bool.TryParse(config["isDebug"], out isDebug))
                    IsDebugMode = isDebug;              
            }

            base.Initialize(name, config);
        }

		public void RegisterDependencies(Control dependantControl, ClientDependencyCollection dependencies, HashSet<IClientDependencyPath> paths)
		{
			DependantControl = dependantControl;
			AllDependencies = new List<IClientDependencyFile>(dependencies);
			FolderPaths = paths;

			UpdateFilePaths();

			List<IClientDependencyFile> jsDependencies = AllDependencies
				.Where(x => x.DependencyType == ClientDependencyType.Javascript)
				.ToList();

            List<IClientDependencyFile> cssDependencies = AllDependencies
				.Where(x => x.DependencyType == ClientDependencyType.Css)
				.ToList();

			// sort by priority
			jsDependencies.Sort((a, b) => a.Priority.CompareTo(b.Priority));
			cssDependencies.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            RegisterCssFiles(cssDependencies.ConvertAll<IClientDependencyFile>(a => { return (IClientDependencyFile)a; }));
			RegisterJsFiles(jsDependencies.ConvertAll<IClientDependencyFile>(a => { return (IClientDependencyFile)a; }));

		}

	

		/// <summary>
		/// Returns a list of urls. 
		/// For the optimized files, the full url with the encoded query strings for the handler which will process the composite list
		/// of dependencies. The handler will compbine, compress, minify (if JS), and output cache the results
		/// based on a hash key of the base64 encoded string.
		/// </summary>
		/// <param name="dependencies"></param>
		/// <param name="groupName"></param>
		/// <returns></returns>
		public List<string> ProcessCompositeList(List<IClientDependencyFile> dependencies, ClientDependencyType type)
		{
			List<string> rVal = new List<string>();
			if (dependencies.Count == 0)
				return rVal;

			//build the combined composite list url
			string handler = "{0}?s={1}&t={2}";			
			StringBuilder files = new StringBuilder();
			foreach (IClientDependencyFile a in dependencies)
			{
				files.Append(a.FilePath + ";");
			}
			string combinedurl = string.Format(handler, ClientDependencySettings.Instance.CompositeFileHandlerPath, HttpContext.Current.Server.UrlEncode(EncodeTo64(files.ToString())), type.ToString());
            rVal.Add(AppendVersionQueryString(combinedurl)); //append our version to the combined url

			//add any urls that are not to be optimized, add the version string to them
			foreach (IClientDependencyFile a in dependencies)
			{
				rVal.Add(AppendVersionQueryString(a.FilePath));
			}
			
			//if (url.Length > CompositeDependencyHandler.MaxHandlerUrlLength)
			//    throw new ArgumentOutOfRangeException("The number of files in the composite group " + groupName + " creates a url handler address that exceeds the CompositeDependencyHandler MaxHandlerUrlLength. Reducing the amount of files in this composite group should fix the issue");
			return rVal;
		}

		private string EncodeTo64(string toEncode)
		{
			byte[] toEncodeAsBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(toEncode);
			string returnValue = System.Convert.ToBase64String(toEncodeAsBytes);
			return returnValue;
		}

		/// <summary>
		/// Ensures the correctly resolved file path is set for each dependency (i.e. so that ~ are taken care of) and also
		/// prefixes the file path with the correct base path specified for the PathNameAlias if specified.
		/// </summary>
		/// <param name="dependencies"></param>
		/// <param name="paths"></param>
		/// <param name="control"></param>
		private void UpdateFilePaths()
		{
			foreach (IClientDependencyFile dependency in AllDependencies)
			{
				if (!string.IsNullOrEmpty(dependency.PathNameAlias))
				{
					List<IClientDependencyPath> paths = FolderPaths.ToList();
					IClientDependencyPath path = paths.Find(
						delegate(IClientDependencyPath p)
						{
							return p.Name == dependency.PathNameAlias;
						}
					);
					if (path == null)
					{
						throw new NullReferenceException("The PathNameAlias specified for dependency " + dependency.FilePath + " does not exist in the ClientDependencyPathCollection");
					}
					string basePath = path.ResolvedPath.EndsWith("/") ? path.ResolvedPath : path.ResolvedPath + "/";
					dependency.FilePath = basePath + dependency.FilePath;
				}
				else
				{
					dependency.FilePath = DependantControl.ResolveUrl(dependency.FilePath);
				}

                //append query strings to each file if we are in debug mode
                if (ClientDependencySettings.Instance.IsDebugMode)
                {
                    dependency.FilePath = AppendVersionQueryString(dependency.FilePath);
                }
			}
		}

        private string AppendVersionQueryString(string url)
        {
            if (ClientDependencySettings.Instance.Version == 0)
                return url;

            //ensure there's not duplicated query string syntax
            url += url.Contains('?') ? "&" : "?";
            //append a version
            url += "cdv=" + ClientDependencySettings.Instance.Version.ToString();
            return url;
        }
		

	}
}