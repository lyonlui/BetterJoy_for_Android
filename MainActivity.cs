using System;

using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using System.Collections.Generic;
using System.Timers;

using PCLAppConfig;
using PCLAppConfig.FileSystemStream;

namespace Joycon
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {


        public TextView console;

        private Button con1;
        private Button con4;
        private Button con3;
        private Button con2;
        private Button loc4;
        private Button loc3;
        private Button loc2;
        private Button loc1;

        public bool nonOriginal;
        public List<Button> con, loc;
        public bool calibrate;
        public List<KeyValuePair<string, float[]>> caliData;
        private Timer countDown;
        private int count;
        public List<int> xG, yG, zG, xA, yA, zA;

        protected override void OnCreate(Bundle savedInstanceState)
        {


            if (ConfigurationManager.AppSettings == null)
                ConfigurationManager.Initialise(PortableStream.Current);


            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

			nonOriginal = Boolean.Parse(ConfigurationManager.AppSettings["NonOriginalController"]);


			xG = new List<int>(); yG = new List<int>(); zG = new List<int>();
            xA = new List<int>(); yA = new List<int>(); zA = new List<int>();
            caliData = new List<KeyValuePair<string, float[]>> {
                new KeyValuePair<string, float[]>("0", new float[6] {0,0,0,-710,0,0})
            };
			
			

			con1 = new Button(this);
			con1.SetBackgroundResource(Resource.Drawable.cross);

			con2 = new Button(this);
			con2.SetBackgroundResource(Resource.Drawable.cross);

			con3 = new Button(this);
			con3.SetBackgroundResource(Resource.Drawable.cross);

			con4 = new Button(this);
			con4.SetBackgroundResource(Resource.Drawable.cross);

			loc1 = new Button(this);
			con1.Text = "Locate";

			loc2 = new Button(this);
			con2.Text = "Locate";

			loc3 = new Button(this);
			con3.Text = "Locate";

			loc4 = new Button(this);
			con4.Text = "Locate";

			/*
			 * con1 = FindViewById<Button>(Resource.Id.con1);
			con2 = FindViewById<Button>(Resource.Id.con2);
			con3 = FindViewById<Button>(Resource.Id.con3);
			con4 = FindViewById<Button>(Resource.Id.con4);

			loc1 = FindViewById<Button>(Resource.Id.loc1);
			loc2 = FindViewById<Button>(Resource.Id.loc2);
			loc3 = FindViewById<Button>(Resource.Id.loc3);
			loc4 = FindViewById<Button>(Resource.Id.loc4);*/


			con = new List<Button> { con1, con2, con3, con4 };
            loc = new List<Button> { loc1, loc2, loc3, loc4 };

            console = this.FindViewById<TextView>(Resource.Id.console) ;


			Config.Init(caliData, PackageName);

			Program.Start(this);

		}


		protected override void OnDestroy()
		{
			base.OnDestroy();
			Program.Stop();
		}

		public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }



        public void AppendTextBox(string value)
        { // https://stackoverflow.com/questions/519233/writing-to-a-textbox-from-another-thread
			/*
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(AppendTextBox), new object[] { value });
                return;
            }*/
            console.Append(value);
        }

        public float[] activeCaliData(string serNum)
        {
            for (int i = 0; i < this.caliData.Count; i++)
            {
                if (this.caliData[i].Key == serNum)
                {
                    return this.caliData[i].Value;
                }
            }
            return this.caliData[0].Value;
        }


		public void locBtnClick(object sender, EventArgs e)
		{
			Button bb = sender as Button;

			if (bb.Tag.GetType() == typeof(Button))
			{
				Button button = bb.Tag as Button;

				if (button.Tag.GetType() == typeof(Joycon))
				{
					Joycon v = (Joycon)button.Tag;
					v.SetRumble(20.0f, 400.0f, 1.0f, 300);
				}
			}
		}

		public void conBtnClick(object sender, EventArgs e)
		{
			Button button = sender as Button;

			if (button.Tag.GetType() == typeof(Joycon))
			{
				Joycon v = (Joycon)button.Tag;

				if (v.other == null && !v.isPro)
				{ // needs connecting to other joycon (so messy omg)
					bool succ = false;

					if (Program.mgr.j.Count == 1)
					{ // when want to have a single joycon in vertical mode
						v.other = v; // hacky; implement check in Joycon.cs to account for this
						succ = true;
					}
					else
					{
						foreach (Joycon jc in Program.mgr.j)
						{
							if (!jc.isPro && jc.isLeft != v.isLeft && jc != v && jc.other == null)
							{
								v.other = jc;
								jc.other = v;

								//Set both Joycon LEDs to the one with the lowest ID
								byte led = jc.LED <= v.LED ? jc.LED : v.LED;
								jc.LED = led;
								v.LED = led;
								jc.SetPlayerLED(led);
								v.SetPlayerLED(led);

								//v.xin.Disconnect();
								//v.xin = null;

								// setting the other joycon's button image
								foreach (Button b in con)
									if (b.Tag == (Object)jc)
										//b.BackgroundImage = jc.isLeft ? Properties.Resources.jc_left : Properties.Resources.jc_right;
										b.SetBackgroundResource(jc.isLeft ? Resource.Drawable.jc_left : Resource.Drawable.jc_right);

								succ = true;
								break;
							}
						}
					}

					if (succ)
						foreach (Button b in con)
							if (b.Tag == (Object)v)
								//b.BackgroundImage = v.isLeft ? Properties.Resources.jc_left : Properties.Resources.jc_right;
								b.SetBackgroundResource(v.isLeft ? Resource.Drawable.jc_left : Resource.Drawable.jc_right);
				}
				else if (v.other != null && !v.isPro)
				{ // needs disconnecting from other joycon
					/*
					if (v.xin == null)
					{
						ReenableXinput(v);
						v.xin.Connect();
					}

					if (v.other.xin == null)
					{
						ReenableXinput(v.other);
						v.other.xin.Connect();
					}
					*/

					//button.BackgroundImage = v.isLeft ? Properties.Resources.jc_left_s : Properties.Resources.jc_right_s;
					button.SetBackgroundResource(v.isLeft ? Resource.Drawable.jc_left_s : Resource.Drawable.jc_right_s);

					foreach (Button b in con)
						if (b.Tag == (Object)v.other)
							//b.BackgroundImage = v.other.isLeft ? Properties.Resources.jc_left_s : Properties.Resources.jc_right_s;
							b.SetBackgroundResource(v.other.isLeft ? Resource.Drawable.jc_left_s : Resource.Drawable.jc_right_s);

					//Set original Joycon LEDs
					v.other.LED = (byte)(0x1 << v.other.PadId);
					v.LED = (byte)(0x1 << v.PadId);
					v.other.SetPlayerLED(v.other.LED);
					v.SetPlayerLED(v.LED);

					v.other.other = null;
					v.other = null;
				}
			}
		}
	}
}