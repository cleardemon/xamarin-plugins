using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Gms.Gcm;
using Android.Media;
using Android.OS;
using Android.Support.V4.App;
using Java.Lang;
using PushNotification.Plugin.Abstractions;


namespace PushNotification.Plugin
{
	/// <summary>
	/// Implementation for Feature
	/// </summary>
	[Service]
	public class PushNotificationImplementation : IntentService, IPushNotification
	{
		const string GcmPreferencesKey = "GCMPreferences";

           

		public IPushNotificationListener Listener { get; set; }

		public string Token { get { return GetRegistrationId(); } }

		public void Register()
		{
			if(!CrossPushNotification.IsInitialized)
			{
              
				throw NewPushNotificationNotInitializedException();
			}
             
			if(string.IsNullOrEmpty(CrossPushNotification.SenderId))
			{


				System.Diagnostics.Debug.WriteLine(string.Format("{0} - Register - SenderId is missing.", PushNotificationKey.DomainName));
                 
				if(CrossPushNotification.IsInitialized)
				{
					CrossPushNotification.PushNotificationListener.OnError(string.Format("{0} - Register - Sender Id is missing.", PushNotificationKey.DomainName), DeviceType.Android);
				}
				else
				{
					throw NewPushNotificationNotInitializedException();
				}
			}
			else if(string.IsNullOrEmpty(Token))
			{
				System.Diagnostics.Debug.WriteLine(string.Format("{0} - Registering for Push Notifications", PushNotificationKey.DomainName));

				InternalRegister();

			}
			else
			{
				System.Diagnostics.Debug.WriteLine(string.Format("{0} - Already Registered for Push Notifications", PushNotificationKey.DomainName));

			}

		}

		public void Unregister()
		{
			if(!CrossPushNotification.IsInitialized)
			{

				throw NewPushNotificationNotInitializedException();
			}
			InternalUnRegister();
		}

		protected override void OnHandleIntent(Intent intent)
		{
			Bundle extras = intent != null ? intent.Extras : null;

			if(extras != null && !extras.IsEmpty)
			{
				System.Diagnostics.Debug.WriteLine(intent.Action);

				if(intent.Action.Equals(PushNotificationKey.IntentFromGcmMessage))
				{
					/*    StoreRegistrationId(Android.App.Application.Context, extras.GetString("registration_id"));
                    }
                    else
                    {*/
					System.Diagnostics.Debug.WriteLine(string.Format("{0} - Push Received", PushNotificationKey.DomainName));

					System.Diagnostics.Debug.WriteLine(intent.Action);

					var parameters = new Dictionary<string, object>();

					foreach(var key in extras.KeySet())
						parameters.Add(key, extras.Get(key));

					Context context = Android.App.Application.Context;

					if(CrossPushNotification.IsInitialized)
					{
						CrossPushNotification.PushNotificationListener.OnMessage(parameters, DeviceType.Android);
					}
					else
					{
						throw NewPushNotificationNotInitializedException();
					}

					try
					{
						int notifyId = 0;
						string title = context.ApplicationInfo.LoadLabel(context.PackageManager);
						string message = "";
						string tag = "";

						if(!string.IsNullOrEmpty(CrossPushNotification.NotificationContentTextKey) && parameters.ContainsKey(CrossPushNotification.NotificationContentTextKey))
						{
							message = parameters[CrossPushNotification.NotificationContentTextKey].ToString();
						}
						else if(parameters.ContainsKey(PushNotificationKey.Message))
						{
							message = parameters[PushNotificationKey.Message].ToString();
						}
						else if(parameters.ContainsKey(PushNotificationKey.Subtitle))
						{
							message = parameters[PushNotificationKey.Subtitle].ToString();
						}
						else if(parameters.ContainsKey(PushNotificationKey.Text))
						{
							message = parameters[PushNotificationKey.Text].ToString();
						}

						if(!string.IsNullOrEmpty(CrossPushNotification.NotificationContentTitleKey) && parameters.ContainsKey(CrossPushNotification.NotificationContentTitleKey))
						{
							title = parameters[CrossPushNotification.NotificationContentTitleKey].ToString();

						}
						else if(parameters.ContainsKey(PushNotificationKey.Title))
						{

							if(!string.IsNullOrEmpty(message))
							{
								title = parameters[PushNotificationKey.Title].ToString();
							}
							else
							{
								message = parameters[PushNotificationKey.Title].ToString();
							}
						}
						if(parameters.ContainsKey(PushNotificationKey.Id))
						{
							var str = parameters[PushNotificationKey.Id].ToString();
							try
							{
								notifyId = Convert.ToInt32(str);
							}
							catch(System.Exception)
							{
								// Keep the default value of zero for the notify_id, but log the conversion problem.
								System.Diagnostics.Debug.WriteLine("Failed to convert {0} to an interger", str);
							}
						}
						if(parameters.ContainsKey(PushNotificationKey.Tag))
						{
							tag = parameters[PushNotificationKey.Tag].ToString();
						}

						if(!parameters.ContainsKey(PushNotificationKey.Silent) || !System.Boolean.Parse(parameters[PushNotificationKey.Silent].ToString()))
						{
							CreateNotification(title, message, notifyId, tag);
						}

					}
					catch(Java.Lang.Exception ex)
					{
						System.Diagnostics.Debug.WriteLine(ex.ToString());
					}
					catch(System.Exception ex1)
					{
						System.Diagnostics.Debug.WriteLine(ex1.ToString());
					}
				}


			}
			// Release the wake lock provided by the WakefulBroadcastReceiver.
			PushNotificationsReceiver.CompleteWakefulIntent(intent);
		}

		static async System.Threading.Tasks.Task InternalUnRegister()
		{

			System.Diagnostics.Debug.WriteLine(string.Format("{0} - Unregistering push notifications", PushNotificationKey.DomainName));
			GoogleCloudMessaging gcm = GoogleCloudMessaging.GetInstance(Android.App.Application.Context);
			if(gcm == null)
				return;

			await System.Threading.Tasks.Task.Run(() => {
				gcm.Unregister();
			});


               

			if(CrossPushNotification.IsInitialized)
			{
				CrossPushNotification.PushNotificationListener.OnUnregistered(DeviceType.Android);
			}
			else
			{
				throw NewPushNotificationNotInitializedException();
			}
			StoreRegistrationId(Android.App.Application.Context, string.Empty);

		}

		static async System.Threading.Tasks.Task InternalRegister()
		{
			Context context = Android.App.Application.Context;

			System.Diagnostics.Debug.WriteLine(string.Format("{0} - Registering push notifications", PushNotificationKey.DomainName));

			if(CrossPushNotification.SenderId == null)
				throw new ArgumentException("No Sender Id Specified");

			try
			{
				GoogleCloudMessaging gcm = GoogleCloudMessaging.GetInstance(context);
				if(gcm == null)
				{
					System.Diagnostics.Debug.WriteLine("GCM services unavailable, not registering device");
					return;
				}

				string regId = await System.Threading.Tasks.Task.Run(() => {
					try
					{
					return gcm.Register(CrossPushNotification.SenderId);
					}
					catch(System.Exception ex)
					{
						return string.Empty;
					}
				});

				System.Diagnostics.Debug.WriteLine(string.Format("{0} - Device registered, registration ID=" + regId, PushNotificationKey.DomainName));


                
				if(CrossPushNotification.IsInitialized)
				{
					CrossPushNotification.PushNotificationListener.OnRegistered(regId, DeviceType.Android);
				}
				else
				{
					throw NewPushNotificationNotInitializedException();
				}
				// Persist the regID - no need to register again.
				StoreRegistrationId(context, regId);



			}
			catch(System.Exception ex)
			{

				System.Diagnostics.Debug.WriteLine(string.Format("{0} - Error :" + ex.Message, PushNotificationKey.DomainName));
        
				if(CrossPushNotification.IsInitialized)
				{
					CrossPushNotification.PushNotificationListener.OnError(string.Format("{0} - Register - " + ex, PushNotificationKey.DomainName), DeviceType.Android);
				}
				else
				{
					throw NewPushNotificationNotInitializedException();
				}
			}


		}

		static PushNotificationNotInitializedException NewPushNotificationNotInitializedException()
		{
			const string description = "CrossPushNotification Plugin is not initialized. Should initialize before use with CrossPushNotification Initialize method. Example:  CrossPushNotification.Initialize<CrossPushNotificationListener>()";

			return new PushNotificationNotInitializedException(description); 
		}


		static string GetRegistrationId()
		{
			string retVal = "";

			Context context = Android.App.Application.Context;

			ISharedPreferences prefs = GetGCMPreferences(context);

			string registrationId = prefs.GetString(PushNotificationKey.Token, "");

			if(string.IsNullOrEmpty(registrationId))
			{
				System.Diagnostics.Debug.WriteLine(string.Format("{0} - - Registration not found.", PushNotificationKey.DomainName));

				return retVal;
			}
			// Check if app was updated; if so, it must clear the registration ID
			// since the existing registration ID is not guaranteed to work with
			// the new app version.
			int registeredVersion = prefs.GetInt(PushNotificationKey.AppVersion, Integer.MinValue);
			int currentVersion = GetAppVersion(context);
			if(registeredVersion != currentVersion)
			{

				System.Diagnostics.Debug.WriteLine(string.Format("{0} - App version changed.", PushNotificationKey.DomainName));

				return retVal;
			}

			retVal = registrationId;

			return retVal;
		}

		static ISharedPreferences GetGCMPreferences(Context context)
		{
			// This sample app persists the registration ID in shared preferences, but
			// how you store the registration ID in your app is up to you.

			return context.GetSharedPreferences(GcmPreferencesKey, FileCreationMode.Private);
		}

		static int GetAppVersion(Context context)
		{
			try
			{
				PackageInfo packageInfo = context.PackageManager.GetPackageInfo(context.PackageName, 0);
				return packageInfo.VersionCode;
			}
			catch(PackageManager.NameNotFoundException e)
			{
				// should never happen
				throw new RuntimeException("Could not get package name: " + e);
			}

		}

		static void StoreRegistrationId(Context context, string regId)
		{
			ISharedPreferences prefs = GetGCMPreferences(context);
			int appVersion = GetAppVersion(context);

			System.Diagnostics.Debug.WriteLine(string.Format("{0} - Saving regId on app version " + appVersion, PushNotificationKey.DomainName));

			ISharedPreferencesEditor editor = prefs.Edit();
			editor.PutString(PushNotificationKey.Token, regId);
			editor.PutInt(PushNotificationKey.AppVersion, appVersion);
			editor.Commit();
		}

		public static void CreateNotification(string title, string message, int notifyId, string tag)
		{
           
			NotificationCompat.Builder builder;
			Context context = Android.App.Application.Context;

			CrossPushNotification.SoundUri = CrossPushNotification.SoundUri ?? RingtoneManager.GetDefaultUri(RingtoneType.Notification);
			try
			{
                
				if(CrossPushNotification.IconResource == 0)
				{
					CrossPushNotification.IconResource = context.ApplicationInfo.Icon;
				}
				else
				{
					string name = context.Resources.GetResourceName(CrossPushNotification.IconResource);

					if(name == null)
					{
						CrossPushNotification.IconResource = context.ApplicationInfo.Icon;

					}
				}
                 
			}
			catch(Android.Content.Res.Resources.NotFoundException ex)
			{
				CrossPushNotification.IconResource = context.ApplicationInfo.Icon;
				System.Diagnostics.Debug.WriteLine(ex.ToString());
			}


			Intent resultIntent = context.PackageManager.GetLaunchIntentForPackage(context.PackageName);
			resultIntent.PutExtra("NotifyId", notifyId);
			//Intent resultIntent = new Intent(context, typeof(T));
           

             
			// Create a PendingIntent; we're only using one PendingIntent (ID = 0):
			const int pendingIntentId = 0;
			PendingIntent resultPendingIntent = PendingIntent.GetActivity(context, pendingIntentId, resultIntent, PendingIntentFlags.OneShot);
          
			// Build the notification
			builder = new NotificationCompat.Builder(context)
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