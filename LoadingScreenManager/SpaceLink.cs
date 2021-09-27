
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LoadingScreenManager
{
	[KSPAddon(KSPAddon.Startup.MainMenu, false)]
	public class SpaceLink : MonoBehaviour
	{
		void Start()
		{
			if (LoadingScreenManager.cfg._neverShowAgain)
				return;
			foreach (MainMenu m in Resources.FindObjectsOfTypeAll<MainMenu>())
			{
				m.spaceportBtn.Text.text = "Loading Screen Mgr Settings";
				m.spaceportBtn.onTap = Settings.GUIToggleToolbar;
			}
		}



		internal static void lockEverything()
		{
			foreach (MainMenu m in Resources.FindObjectsOfTypeAll<MainMenu>())
			{

				m.startBtn.Lock();
				m.quitBtn.Lock();
				m.commBtn.Lock();
				m.spaceportBtn.Lock();
				m.settingBtn.Lock();
				m.creditsBtn.Lock();
				m.merchandiseBtn.Lock();
				m.unitTestsBtn.interactable = false;
				m.newGameBtn.Lock();
				m.continueBtn.Lock();
				m.trainingBtn.Lock();
				m.scenariosBtn.Lock();
				m.playESAMissionsBtn.Lock();
				m.playMissionsBtn.Lock();
				m.missionBuilderBtn.Lock();
				m.backBtn.Lock();
				m.buyMakingHistoryBtn.Lock();
				m.buySerenityBtn.Lock();
			}
		}

		internal  static void unlockEverything()
		{
			foreach (MainMenu m in Resources.FindObjectsOfTypeAll<MainMenu>())
			{
				m.startBtn.Unlock();
				m.quitBtn.Unlock();
				m.commBtn.Unlock();
				m.spaceportBtn.Unlock();
				m.settingBtn.Unlock();
				m.creditsBtn.Unlock();
				m.merchandiseBtn.Unlock();
				m.unitTestsBtn.interactable = true;
				m.newGameBtn.Unlock();
				m.continueBtn.Unlock();
				m.trainingBtn.Unlock();
				m.scenariosBtn.Unlock();
				m.playESAMissionsBtn.Unlock();
				m.playMissionsBtn.Unlock();
				m.missionBuilderBtn.Unlock();
				m.backBtn.Unlock();
				m.buyMakingHistoryBtn.Unlock();
				m.buySerenityBtn.Unlock();
			}
		}
	}
}
