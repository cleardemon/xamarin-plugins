using System;

namespace PushNotification.Plugin.Abstractions
{
	public interface ICrossPushNotification
	{
		bool IsInitialized { get; }
		IPushNotificationListener PushNotificationListener { get; }
		IPushNotification PushNotification { get; }

	}
}

