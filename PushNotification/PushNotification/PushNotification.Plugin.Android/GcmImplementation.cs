using System;
using Android.App;
using Android.Content;
using Gcm.Client;
using PushNotification.Plugin.Abstractions;
using System.Collections.Generic;
using Android.Media;

[assembly: Permission(Name = "@PACKAGE_NAME@.permission.C2D_MESSAGE")]
[assembly: UsesPermission(Name = "android.permission.WAKE_LOCK")]
[assembly: UsesPermission(Name = "com.google.android.c2dm.permission.RECEIVE")]
[assembly: UsesPermission(Name = "android.permission.INTERNET")]

namespace PushNotification.Plugin
{
	[BroadcastReceiver(Permission=Constants.PERMISSION_GCM_INTENTS)]
	[IntentFilter(new [] { Constants.INTENT_FROM_GCM_MESSAGE }, Categories = new [] { "@PACKAGE_NAME@" })]
	[IntentFilter(new [] { Constants.INTENT_FROM_GCM_REGISTRATION_CALLBACK }, Categories = new [] { "@PACKAGE_NAME@" })]
	[IntentFilter(new [] { Constants.INTENT_FROM_GCM_LIBRARY_RETRY }, Categories = new [] { "@PACKAGE_NAME@" })]
	// Analysis disable once ConvertToStaticType
	public class GcmBroadcastReceiver : GcmBroadcastReceiverBase<PushNotificationImplementation>
	{
		//IMPORTANT: Change this to your own Sender ID!
		//The SENDER_ID is your Google API Console App Project Number
		public static string[] SENDER_IDS = {"318563020124"};
	}

	[Service] //Must use the service tag
	public class PushNotificationImplementation : GcmServiceBase, IPushNotification
	{
		public PushNotificationImplementation() : base(GcmBroadcastReceiver.SENDER_IDS) { }

		string _token;

		public string Token { get { return _token; } }

		public IPushNotificationListener Listener
		{
			get { return CrossPushNotification.PushNotificationListener; }
		}

		public void Register()
		{
			try
			{
				GcmClient.CheckDevice(Application.Context);
				GcmClient.CheckManifest(Application.Context);
				GcmClient.Register(Application.Context, GcmBroadcastReceiver.SENDER_IDS);
			}
			catch(InvalidOperationException)
			{
				// if thrown, push notifications not supported
			}
		}

		public void Unregister()
		{
			GcmClient.UnRegister(Application.Context);
		}

		void CheckListener()
		{
			if(Listener == null)
				throw new NullReferenceException("Listener not initialised");
		}

		protected override void OnRegistered (Context context, string registrationId)
		{
			CheckListener();
			_token = registrationId;
			Listener.OnRegistered(registrationId, DeviceType.Android);
		}

		protected override void OnUnRegistered (Context context, string registrationId)
		{
			CheckListener();
			_token = null;
			Listener.OnUnregistered(DeviceType.Android);
		}

		protected override void OnMessage (Context context, Intent intent)
		{
			var data = new Dictionary<string, object>();
			if(intent != null && intent.Extras != null)
			{
				foreach(var key in intent.Extras.KeySet())
				{
					data.Add(key, intent.Extras.Get(key));
					System.Diagnostics.Debug.WriteLine("Push key: {0} = {1}", key, intent.Extras.Get(key));
				}
			}

			string message = string.Empty;

			if(Listener != null)
			{
				message = Listener.GetMessageText(data, DeviceType.Android);
				Listener.OnMessage(data, DeviceType.Android);
			}

			// build push notification
			try
			{
				int notifyId = 0;
				string title = context.ApplicationInfo.LoadLabel(context.PackageManager);
				string tag = string.Empty;

				if(!string.IsNullOrEmpty(CrossPushNotification.NotificationContentTextKey) && data.ContainsKey(CrossPushNotification.NotificationContentTextKey))
					message = data[CrossPushNotification.NotificationContentTextKey].ToString();
				else if(data.ContainsKey(PushNotificationKey.Message))
					message = data[PushNotificationKey.Message].ToString();
				else if(data.ContainsKey(PushNotificationKey.Subtitle))
					message = data[PushNotificationKey.Subtitle].ToString();
				else if(data.ContainsKey(PushNotificationKey.Text))
					message = data[PushNotificationKey.Text].ToString();

				if(!string.IsNullOrEmpty(CrossPushNotification.NotificationContentTitleKey) && data.ContainsKey(CrossPushNotification.NotificationContentTitleKey))
					title = data[CrossPushNotification.NotificationContentTitleKey].ToString();
				else if(data.ContainsKey(PushNotificationKey.Title))
				{
					if(!string.IsNullOrEmpty(message))
						title = data[PushNotificationKey.Title].ToString();
					else
						message = data[PushNotificationKey.Title].ToString();
				}
				if(data.ContainsKey(PushNotificationKey.Id))
				{
					var str = data[PushNotificationKey.Id].ToString();
					try
					{
						notifyId = Convert.ToInt32(str);
					}
					catch(Exception)
					{
						// Keep the default value of zero for the notify_id, but log the conversion problem.
						System.Diagnostics.Debug.WriteLine("Failed to convert {0} to an interger", str);
					}
				}
				if(data.ContainsKey(PushNotificationKey.Tag))
					tag = data[PushNotificationKey.Tag].ToString();

				if(!data.ContainsKey(PushNotificationKey.Silent) || !Boolean.Parse(data[PushNotificationKey.Silent].ToString()))
					CreateNotification(title, message, notifyId, tag);
			}
			catch(Java.Lang.Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex.ToString());
			}
			catch(Exception ex1)
			{
				System.Diagnostics.Debug.WriteLine(ex1.ToString());
			}
		}

		protected override void OnError (Context context, string errorId)
		{
			CheckListener();
			Listener.OnError(errorId, DeviceType.Android);
		}

		//
		// Notification creation
		//

		public static void CreateNotification(string title, string message, int notifyId, string tag)
		{
			if(notifyId == 0)
			{
				System.Diagnostics.Debug.WriteLine("Not creating notification when id is zero");
				return;
			}

			Notification.Builder builder;
			Context context = Android.App.Application.Context;

			CrossPushNotification.SoundUri = CrossPushNotification.SoundUri ?? RingtoneManager.GetDefaultUri(RingtoneType.Notification);
			try
			{
				if(CrossPushNotification.IconResource == 0)
					CrossPushNotification.IconResource = context.ApplicationInfo.Icon;
				else
				{
					string name = context.Resources.GetResourceName(CrossPushNotification.IconResource);
					if(name == null)
						CrossPushNotification.IconResource = context.ApplicationInfo.Icon;
				}

			}
			catch(Android.Content.Res.Resources.NotFoundException)
			{
				CrossPushNotification.IconResource = context.ApplicationInfo.Icon;
			}

			Intent resultIntent = context.PackageManager.GetLaunchIntentForPackage(context.PackageName);
			resultIntent.PutExtra("NotifyId", notifyId.ToString());
			const int pendingIntentId = 0;
			PendingIntent resultPendingIntent = PendingIntent.GetActivity(context, pendingIntentId, resultIntent, PendingIntentFlags.OneShot);

			// Build the notification
			builder = new Notification.Builder(context)
				.SetAutoCancel(true) // dismiss the notification from the notification area when the user clicks on it
				.SetContentIntent(resultPendingIntent) // start up this activity when the user clicks the intent.
				.SetContentTitle(title) // Set the title
				.SetSound(CrossPushNotification.SoundUri)                           
				.SetSmallIcon(CrossPushNotification.IconResource) // This is the icon to display
				.SetContentText(message); // the message to display.

			var notificationManager = (NotificationManager)context.GetSystemService(Context.NotificationService);
			notificationManager.Notify(tag, notifyId, builder.Build());
		}
	}
}

