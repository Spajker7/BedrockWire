using Avalonia.Controls;
using BedrockWire.Models;
using BedrockWireAuthDump;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace BedrockWire.ViewModels
{
    public class StartProxyDialogViewModel : ViewModelBase
    {
		public bool ShowAuthChoice { get; set; } = true;
		public bool ShowAuthDevice { get; set; } = false;
		public bool ShowSaveAuth { get; set; } = false;
		public bool ShowProxySettings { get; set; } = false;

		public bool NormallyClosing { get; set; } = false;

		public string DeviceCode { get; set; }
		public string UserCode { get; set; }
		public string VerificationUrl { get; set; }
        public string AuthDeviceError { get; set; }
		public string RemoteServerAddress { get; set; } = "127.0.0.1:19132";
		public int ProxyPort { get; set; } = 19135;

        public ProxyAuthenticationResult ProxyAuthenticationResult { get; set; }

        public async void OnOpenAuthCommand(Window window)
        {
			var dlg = new OpenFileDialog();
			dlg.Filters.Add(new FileDialogFilter() { Name = "JSON Files", Extensions = { "json" } });
			dlg.AllowMultiple = false;

			var result = await dlg.ShowAsync(window);
            if (result != null && result.Length > 0)
            {
                ProxyAuthenticationResult authResult;

                using (StreamReader sr = new StreamReader(result[0]))
                {
                    string authJson = sr.ReadToEnd();
                    authResult = JsonConvert.DeserializeObject<ProxyAuthenticationResult>(authJson);
                }

                if (!XboxAuthService.IsMinecraftChainValid(authResult.MinecraftChain))
                {
                    if (!XboxAuthService.IsAccessTokenValid(authResult.Tokens))
                    {
                        XboxAuthService authService = new XboxAuthService();
						string deviceId = authResult.Tokens.DeviceId;
                        authResult.Tokens = await authService.RefreshAccessToken(authResult.Tokens.RefreshToken);
						authResult.Tokens.DeviceId = deviceId;
                    }

                    if (!XboxAuthService.IsAccessTokenValid(authResult.Tokens))
                    {
                        return; // if still not valid after refresh... tough luck
                    }

					AuthDump authDump = new AuthDump();
					var res = await authDump.GetMinecraftChain(authResult.Tokens.AccessToken, authResult.Tokens.DeviceId);

					if (!res.success)
					{
						return;
					}

					var keys = AuthDump.SerializeKeys(res.minecraftKeyPair);

					authResult.PrivateKey = keys.priv;
					authResult.PublicKey = keys.pub;

					// update saved config after token refresh
					using (StreamWriter sr = new StreamWriter(result[0]))
					{
						sr.Write(JsonConvert.SerializeObject(authResult));
					}
				}

				ProxyAuthenticationResult = authResult;
				ShowAuthChoice = false;
				this.RaisePropertyChanged(nameof(ShowAuthChoice));
				FinishAuth();
			}
        }

        public async void OnDeviceAuthCommand(Window window)
        {
            AuthDump authDump = new AuthDump();
            var result = await authDump.RequestDeviceCode();

			DeviceCode = result.deviceCode;
			UserCode = "Code: " + result.userCode;
			this.RaisePropertyChanged(nameof(UserCode));
			VerificationUrl = result.verificationUrl;
			this.RaisePropertyChanged(nameof(VerificationUrl));

			ShowAuthChoice = false;
            this.RaisePropertyChanged(nameof(ShowAuthChoice));
            ShowAuthDevice = true;
            this.RaisePropertyChanged(nameof(ShowAuthDevice));

			var tokenResult = await authDump.StartDeviceCodePolling(DeviceCode);
			if(tokenResult == null)
            {
				AuthDeviceError = "Failed to login.";
				this.RaisePropertyChanged(nameof(AuthDeviceError));
				return;
            }

			var auth = await authDump.GetMinecraftChain(tokenResult.AccessToken, tokenResult.DeviceId);

			if(!auth.success)
            {
				AuthDeviceError = "Failed to obtain Minecraft Chain.";
				this.RaisePropertyChanged(nameof(AuthDeviceError));
				return;
			}

			var keys = AuthDump.SerializeKeys(auth.minecraftKeyPair);

			List<string> chain = new List<string>();
			dynamic json = JObject.Parse(auth.minecraftChain);

			JArray chainArr = json.chain;

			foreach (JToken token in chainArr)
            {
				chain.Add(token.ToString());
            }

			ProxyAuthenticationResult = new ProxyAuthenticationResult()
			{
				Tokens = tokenResult,
				PrivateKey = keys.priv,
				PublicKey = keys.pub,
				MinecraftChain = chain.ToArray()
			};

			ShowAuthDevice = false;
			this.RaisePropertyChanged(nameof(ShowAuthDevice));
			ShowSaveAuth = true;
			this.RaisePropertyChanged(nameof(ShowSaveAuth));
		}
		public async void OnOpenVerificationCommand()
		{
			OpenBrowser(VerificationUrl);
		}

		public void FinishAuth()
        {
			ShowProxySettings = true;
			this.RaisePropertyChanged(nameof(ShowProxySettings));
		}

		public async void OnSaveAuthCommand(Window window)
		{
			SaveFileDialog SaveFileBox = new SaveFileDialog();
			SaveFileBox.Title = "Save Document As...";
			SaveFileBox.InitialFileName = "auth.json";
			List<FileDialogFilter> Filters = new List<FileDialogFilter>();
			FileDialogFilter filter = new FileDialogFilter();
			List<string> extension = new List<string>();
			extension.Add("json");
			filter.Extensions = extension;
			filter.Name = "JSON Files";
			Filters.Add(filter);
			SaveFileBox.Filters = Filters;

			SaveFileBox.DefaultExtension = "json";

			var fileName = await SaveFileBox.ShowAsync(window);
			using (StreamWriter writer = new StreamWriter(fileName))
			{
				writer.Write(JsonConvert.SerializeObject(ProxyAuthenticationResult));
			}

			ShowSaveAuth = false;
			this.RaisePropertyChanged(nameof(ShowSaveAuth));
			FinishAuth();
		}

		public async void OnNotSaveAuthCommand(Window window)
		{
			ShowSaveAuth = false;
			this.RaisePropertyChanged(nameof(ShowSaveAuth));
			FinishAuth();
		}

		public async void OnStartProxyCommand(Window window)
		{
			ProxySettings proxySettings = new ProxySettings();
			proxySettings.RemoteServerAddress = RemoteServerAddress;
			proxySettings.ProxyPort = ProxyPort;
			proxySettings.Auth = new AuthDataSerialized();
			proxySettings.Auth.Chain = ProxyAuthenticationResult.MinecraftChain;
			proxySettings.Auth.PrivateKey = ProxyAuthenticationResult.PrivateKey;
			proxySettings.Auth.PublicKey = ProxyAuthenticationResult.PublicKey;

			NormallyClosing = true;
			window.Close(proxySettings);
		}

		private static void OpenBrowser(string url)
		{
			try
			{
				Process.Start(url);
			}
			catch
			{
				// hack because of this: https://github.com/dotnet/corefx/issues/10361
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					url = url.Replace("&", "^&");
					Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					Process.Start("xdg-open", url);
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					Process.Start("open", url);
				}
				else
				{
					throw;
				}
			}
		}

	}
}
