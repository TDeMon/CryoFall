﻿namespace AtomicTorch.CBND.CoreMod.UI.Controls.Game.Respawn
{
    using System.Windows.Media.Animation;
    using AtomicTorch.CBND.CoreMod.ClientComponents.Input;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core.Menu;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Respawn.Data;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Menu;
    using AtomicTorch.CBND.CoreMod.UI.Helpers;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.ServicesClient;
    using AtomicTorch.GameEngine.Common.Client.MonoGame.UI;

    public partial class WindowRespawn : BaseUserControl
    {
        private static ClientInputContext inputContext;

        private static WindowRespawn instance;

        private static bool isClosed = true;

        private Storyboard storyboardFadeIn;

        public new static bool IsOpened => instance != null;

        public static void EnsureClosed()
        {
            if (isClosed)
            {
                return;
            }

            isClosed = true;
            instance?.CloseWindow();
        }

        public static void EnsureOpened()
        {
            if (!isClosed)
            {
                return;
            }

            isClosed = false;
            var windowRespawn = new WindowRespawn();
            instance = windowRespawn;

            Api.Client.UI.LayoutRootChildren.Add(instance);

            var loadingSplashScreenState = LoadingSplashScreenManager.Instance.CurrentState;
            var delay = loadingSplashScreenState == LoadingSplashScreenState.Shown
                        || loadingSplashScreenState == LoadingSplashScreenState.Showing
                            ? 0
                            : 2;
            ClientTimersSystem.AddAction(
                delaySeconds: delay,
                action: () =>
                        {
                            if (isClosed)
                            {
                                if (ReferenceEquals(instance, windowRespawn))
                                {
                                    instance = null;
                                    Api.Client.UI.LayoutRootChildren.Remove(instance);
                                }

                                return;
                            }

                            instance.Open();
                        });
        }

        protected override void InitControl()
        {
            this.DataContext = new ViewModelWindowRespawn(
                callbackRefreshHeght: () => { });

            // special hack for NoesisGUI animation completed event
            this.Tag = this;

            var loadingSplashScreenState = LoadingSplashScreenManager.Instance.CurrentState;
            if (loadingSplashScreenState == LoadingSplashScreenState.Shown
                || loadingSplashScreenState == LoadingSplashScreenState.Showing)
            {
                // no fade-in required
                return;
            }

            this.storyboardFadeIn = AnimationHelper.CreateStoryboard(
                this,
                durationSeconds: 2,
                from: 0,
                to: 1,
                propertyName: OpacityProperty.Name);
            this.Opacity = 0;
        }

        private void BackgroundFadeOutCompleted()
        {
            Api.Client.UI.LayoutRootChildren.Remove(this);
            inputContext?.Stop();
            inputContext = null;
        }

        private void CloseWindow()
        {
            this.storyboardFadeIn?.Stop(this);

            AnimationHelper.CreateStoryboard(
                               this,
                               durationSeconds: 0.667,
                               from: this.Opacity,
                               to: 0,
                               propertyName: OpacityProperty.Name,
                               onCompleted: this.BackgroundFadeOutCompleted)
                           .Begin(this);

            if (instance == this)
            {
                instance = null;
            }
        }

        private void Open()
        {
            this.storyboardFadeIn?.Begin(this);
            Menu.CloseAll();

            // ReSharper disable once CanExtractXamlLocalizableStringCSharp
            inputContext = ClientInputContext.Start("Respawn menu - intercept all other input")
                                             .HandleAll(
                                                 () =>
                                                 {
                                                     if (ClientInputManager.IsButtonDown(GameButton.CancelOrClose))
                                                     {
                                                         MainMenuOverlay.Toggle();
                                                     }

                                                     ClientInputManager.ConsumeAllButtons();
                                                 });
        }
    }
}