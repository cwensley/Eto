using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#if NET
[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(Eto.HotReloadService))]

namespace Eto;

public static class HotReloadService
{
	static event Action<Type[]> Update; 

	public static void Initialize()
	{
		if (!Debugger.IsAttached)
			return;
			

		Eto.Style.Add<Container>(null, container =>
		{
			var initializeControls = container.GetType().GetMethod("InitializeControls", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
			if (initializeControls == null)
				return;

			void DoUpdate(Type[]? types)
			{
				try
				{
					if (types.Contains(container.GetType()))
					{
						initializeControls?.Invoke(container, null);
					}
				}
				catch
				{
				}
			};
			container.Load += (sender, e) => Update += DoUpdate;
			container.UnLoad += (sender, e) => Update -= DoUpdate;
		});
	}

	internal static void ClearCache(Type[]? updatedTypes)
    {
    }

    internal static void UpdateApplication(Type[]? updatedTypes)
    {
		Application.Instance?.Invoke(() => Update?.Invoke(updatedTypes));
    }
}

#endif
