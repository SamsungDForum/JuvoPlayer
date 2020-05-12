/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Threading.Tasks;
using JuvoLogger;
using Xamarin.Forms;
using XamarinPlayer.Tizen.TV.Services;
using XamarinPlayer.Tizen.TV.Views;

namespace XamarinPlayer.Tizen.TV
{
    public class App : Application
    {
        private string _deepLinkUrl;
        private bool _isInForeground;
        
        private static Task _prepareTask;
        private static NavigationPage AppMainPage { get; set; }

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public App()
        {
            MainPage = new NavigationPage();
            AppMainPage = MainPage as NavigationPage;
            var loadingScreenPage = new LoadingScreen();
            var loadingScreenTask = AppMainPage.PushAsync(loadingScreenPage);
            _prepareTask = PrepareContent(loadingScreenPage, loadingScreenTask);
        }

        protected override void OnStart()
        {
            // Handle when your app starts
        }

        protected override void OnSleep()
        {
            Logger.Info("");
            if (AppMainPage.CurrentPage is ISuspendable suspendable)
                suspendable.Suspend();
            _isInForeground = false;
        }

        protected override void OnResume()
        {
            Logger.Info("");
            if (AppMainPage.CurrentPage is ISuspendable suspendable)
                suspendable.Resume();
            _isInForeground = true;

            if (_deepLinkUrl == null)
                return;
#pragma warning disable 4014
            LoadUrlImpl(_deepLinkUrl);
#pragma warning restore 4014
            _deepLinkUrl = null;
        }

        public async Task PrepareContent(Page loadingScreenPage, Task loadingScreenTask)
        {
            await Task.Yield();
            var contentListPage = new ContentListPage(AppMainPage);
            await loadingScreenTask;
            AppMainPage.Navigation.InsertPageBefore(contentListPage,loadingScreenPage);
            await AppMainPage.Navigation.PopAsync(true);
        }
        public Task LoadUrl(string url)
        {
            if (_isInForeground)
            {
                _deepLinkUrl = null;
                return LoadUrlImpl(url);
            }

            _deepLinkUrl = url;
            return Task.CompletedTask;
        }

        private static async Task LoadUrlImpl(string url)
        {
            await _prepareTask;
            Logger.Info("");
            while (true)
            {
                if (AppMainPage.CurrentPage is IContentPayloadHandler handler && handler.HandleUrl(url))
                    return;
                var page = await AppMainPage.PopAsync();
                if (page == null)
                    return;
            }
        }
    }
}
