using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Timers;
using System.Runtime.InteropServices;

using Android.Widget;

using static Joycon.HIDapi;
using PCLAppConfig;

namespace Joycon
{
    public class JoyconManager
    {

		public bool EnableIMU = true;
		public bool EnableLocalize = false;

		private const ushort vendor_id = 0x57e;
		private const ushort product_l = 0x2006;
		private const ushort product_r = 0x2007;
		private const ushort product_pro = 0x2009;
		private const ushort product_snes = 0x2017;

		public List<Joycon> j; // Array of all connected Joy-Cons
		static JoyconManager instance;

		public MainActivity mActivity;

		Timer controllerCheck;

		public static JoyconManager Instance
		{
			get { return instance; }
		}

		public void Awake()
		{
			instance = this;
			j = new List<Joycon>();
			HIDapi.hid_init();
		}

		public void Start()
		{
			controllerCheck = new System.Timers.Timer(2000); // check for new controllers every 2 seconds
			controllerCheck.Elapsed += CheckForNewControllersTime;
			controllerCheck.Start();
		}

		bool ControllerAlreadyAdded(string path)
		{
			foreach (Joycon v in j)
				if (v.path == path)
					return true;
			return false;
		}

		void CleanUp()
		{ // removes dropped controllers from list
			List<Joycon> rem = new List<Joycon>();
			for (int i = 0; i < j.Count; i++)
			{
				Joycon v = j[i];
				if (v.state == Joycon.state_.DROPPED)
				{
					if (v.other != null)
						v.other.other = null; // The other of the other is the joycon itself

					v.Detach(); rem.Add(v);

					foreach (Button b in mActivity.con)
					{
						if (b.Enabled & b.Tag == (Object)v)
						{

							b.SetBackgroundColor(Android.Graphics.Color.Gray); //= System.Drawing.Color.FromArgb(0x00, System.Drawing.SystemColors.Control);
							b.Enabled = false;
							b.SetBackgroundResource(Resource.Drawable.cross);
							break;
						}
					}

					mActivity.AppendTextBox("Removed dropped controller to list. Can be reconnected.\r\n");
				}
			}

			foreach (Joycon v in rem)
				j.Remove(v);
		}

		void CheckForNewControllersTime(Object source, ElapsedEventArgs e)
		{
			if (Config.IntValue("ProgressiveScan") == 1)
			{
				CheckForNewControllers();
			}
		}

		public void CheckForNewControllers()
		{
			CleanUp();

			// move all code for initializing devices here and well as the initial code from Start()
			bool isLeft = false;
			IntPtr ptr = HIDapi.hid_enumerate(vendor_id, 0x0);
			//IntPtr top_ptr = ptr;

			hid_device_info enumerate; // Add device to list
			bool foundNew = false;
			while (ptr != IntPtr.Zero)
			{
				enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));

				if (enumerate.serial_number == null)
				{
					ptr = enumerate.next; // can't believe it took me this long to figure out why USB connections used up so much CPU.
										  // it was getting stuck in an inf loop here!
					continue;
				}

				if (mActivity.nonOriginal)
				{
					enumerate.product_id = product_pro;
				}

				bool validController = (enumerate.product_id == product_l || enumerate.product_id == product_r ||
										enumerate.product_id == product_pro || enumerate.product_id == product_snes);
				if (validController && !ControllerAlreadyAdded(enumerate.path))
				{
					switch (enumerate.product_id)
					{
						case product_l:
							isLeft = true;
							mActivity.AppendTextBox("Left Joy-Con connected.\r\n"); break;
						case product_r:
							isLeft = false;
							mActivity.AppendTextBox("Right Joy-Con connected.\r\n"); break;
						case product_pro:
							isLeft = true;
							mActivity.AppendTextBox("Pro controller connected.\r\n"); break;
						case product_snes:
							isLeft = true;
							mActivity.AppendTextBox("SNES controller connected.\r\n"); break;
						default:
							mActivity.AppendTextBox("Non Joy-Con Nintendo input device skipped.\r\n"); break;
					}

					IntPtr handle = HIDapi.hid_open_path(enumerate.path);
					try
					{
						HIDapi.hid_set_nonblocking(handle, 1);
					}
					catch
					{
						mActivity.AppendTextBox("Unable to open path to device - are you using the correct (64 vs 32-bit) version for your PC?\r\n");
						break;
					}

					bool isPro = enumerate.product_id == product_pro;
					bool isSnes = enumerate.product_id == product_snes;
					j.Add(new Joycon(handle, EnableIMU, EnableLocalize & EnableIMU, 0.05f, isLeft, enumerate.path, enumerate.serial_number, j.Count, isPro, isSnes));

					foundNew = true;
					j.Last().mActivity = mActivity;

					if (j.Count < 5)
					{
						int ii = -1;
						foreach (Button v in mActivity.con)
						{
							ii++;
							if (!v.Enabled)
							{
								int temp;
								switch (enumerate.product_id)
								{
									case (product_l):
										temp = Resource.Drawable.jc_left_s; break;	
									case (product_r):
										temp = Resource.Drawable.jc_right_s; break;
									case (product_pro):
										temp = Resource.Drawable.pro; break;
									case (product_snes):
										temp = Resource.Drawable.snes; break;
									default:
										temp = Resource.Drawable.cross; break;
								}

								

								
									v.Tag = j.Last(); // assign controller to button
									v.Enabled = true;
									v.Click += new EventHandler(mActivity.conBtnClick);
									
									v.SetBackgroundResource(temp);
							

								
									mActivity.loc[ii].Tag = v;
									mActivity.loc[ii].Click += new EventHandler(mActivity.locBtnClick);
							

								break;
							}
						}
					}

					byte[] mac = new byte[6];
					Console.WriteLine(string.Format("mac address =>      {0}", enumerate.serial_number));
					//onsole.WriteLine("mac address => {0} , len => {1}",, enumerate.serial_number.Length);
					for (int n = 0; n < 6; n++)
						mac[n] = byte.Parse(enumerate.serial_number.Substring(n * 2, 2), System.Globalization.NumberStyles.HexNumber); //TODO
						
					
						
					j[j.Count - 1].PadMacAddress = new PhysicalAddress(mac);
				}

				ptr = enumerate.next;
			}




			if (foundNew)
			{ // attempt to auto join-up joycons on connection
				Joycon temp = null;
				foreach (Joycon v in j)
				{
					if (!v.isPro)
					{
						if (temp == null)
							temp = v;
						else if (temp.isLeft != v.isLeft && v.other == null)
						{
							temp.other = v;
							v.other = temp;

							//Set both Joycon LEDs to the one with the lowest ID
							byte led = temp.LED <= v.LED ? temp.LED : v.LED;
							temp.LED = led;
							v.LED = led;
							temp.SetPlayerLED(led);
							v.SetPlayerLED(led);
							/*
							if (temp.xin != null)
							{
								try
								{
									temp.xin.Disconnect();
								}
								catch (Exception e)
								{
									// it wasn't connected in the first place, go figure
								}
							}
							if (temp.ds4 != null)
							{
								try
								{
									temp.ds4.Disconnect();
								}
								catch (Exception e)
								{
									// it wasn't connected in the first place, go figure
								}
							}
							temp.xin = null;
							temp.ds4 = null;
							*/
							
							foreach (Button b in mActivity.con)
								
								if (b.Tag == (Object)v || b.Tag == (Object)temp)
								{
									Joycon tt = (b.Tag == (Object)v) ? v : (b.Tag == (Object)temp) ? temp : v;
									//b.BackgroundImage = tt.isLeft ? Properties.Resources.jc_left : Properties.Resources.jc_right;
									b.SetBackgroundResource(tt.isLeft ? Resource.Drawable.jc_left : Resource.Drawable.jc_right);
									
								}

							temp = null;    // repeat
						}
					}
				}
			}

			HIDapi.hid_free_enumeration(ptr);

			foreach (Joycon jc in j)
			{ // Connect device straight away
				if (jc.state == Joycon.state_.NOT_ATTACHED)
				{
					/*
					if (jc.xin != null)
						jc.xin.Connect();
					if (jc.ds4 != null)
						jc.ds4.Connect();
					*/
					jc.Attach(leds_: jc.LED);

					bool on = ConfigurationManager.AppSettings["HomeLEDOn"].ToLower() == "true";  //TODO
					foreach (Joycon j in Program.mgr.j)
					{
						j.SetHomeLight(on);
					}

					jc.Begin();
					if (mActivity.nonOriginal)
					{
						jc.getActiveData();
					}

				}
			}
		}

		public void Update()
		{
			for (int i = 0; i < j.Count; ++i)
				j[i].Update();
		}

		public void OnApplicationQuit()
		{
			foreach (Joycon v in j)
			{
				if (Boolean.Parse(ConfigurationManager.AppSettings["AutoPowerOff"]))
					v.PowerOff();
				else
					v.Detach();

				/*
				if (v.xin != null)
				{
					v.xin.Disconnect();
				}

				if (v.ds4 != null)
				{
					v.ds4.Disconnect();
				}*/
			}

			controllerCheck.Stop();
			HIDapi.hid_exit();
		}
	}
}