using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Net;

using Android.OS;


using PCLAppConfig;


namespace Joycon
{

	// Custom timer class because system timers have a limit of 15.6ms
	class HighResTimer
	{
		double interval = 0;
		double frequency = 0;

		Thread thread;

		public delegate void ActionDelegate();
		ActionDelegate func;

		bool run = false;

		public HighResTimer(double f, ActionDelegate a)
		{
			frequency = f;
			interval = 1.0 / f;

			func = a;
		}

		public void Start()
		{
			run = true;
			thread = new Thread(new ThreadStart(Run));
			thread.IsBackground = true;
			thread.Start();
		}

		void Run()
		{
			while (run)
			{
				func();
				int timeToSleep = (int)(interval * 1000);
				Thread.Sleep(timeToSleep);
			}
		}

		public void Stop()
		{
			run = false;
		}
	}


	public class Program
    {
		public static PhysicalAddress btMAC = new PhysicalAddress(new byte[] { 0, 0, 0, 0, 0, 0 });
		public static UdpServer server;
		static double pollsPerSecond = 120.0;

		

		//private static readonly HttpClient client = new HttpClient();

		public static JoyconManager mgr;
		static HighResTimer timer;
		static string pid;

		static MainActivity mActivity;

		static public bool useHIDG = Boolean.Parse(ConfigurationManager.AppSettings["UseHIDG"]);

		//private static WindowsInput.Events.Sources.IKeyboardEventSource keyboard;
		//private static WindowsInput.Events.Sources.IMouseEventSource mouse;

		public static void Start(MainActivity m)
		{

			Program.mActivity = m;

			pid = Process.MyPid().ToString(); // get current process id for HidCerberus.Srv
		
			foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
			{
				// Get local BT host MAC
				if (nic.NetworkInterfaceType != NetworkInterfaceType.FastEthernetFx && nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
				{
					if (nic.Name.Split()[0] == "Bluetooth")
					{
						btMAC = nic.GetPhysicalAddress();
					}
				}
			}

			mgr = new JoyconManager();
			mgr.mActivity = mActivity;
			mgr.Awake();
			mgr.CheckForNewControllers();
			mgr.Start();

			server = new UdpServer(mgr.j);
			server.mActivity = mActivity;

			server.Start(IPAddress.Parse(ConfigurationManager.AppSettings["IP"]), Int32.Parse(ConfigurationManager.AppSettings["Port"]));
			timer = new HighResTimer(pollsPerSecond, new HighResTimer.ActionDelegate(mgr.Update));
			timer.Start();

			mActivity.console.Append("All systems go\r\n");
		}
		
		public static void Stop()
		{
			server.Stop();
			timer.Stop();
			mgr.OnApplicationQuit();
		}

	
	}

}