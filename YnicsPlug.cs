using Facepunch.Extend;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins{
    [Info("Ynic's plugin","Ynic", "0.0.1")]
    class YnicsPlug : RustPlugin {
        [ChatCommand("timer")]
        void startTimer(BasePlayer player, string command, string[] args) {

            //Проверка на админа
            if (!IsAdmin(player)) {
                player.ChatMessage("У вас не хватает прав");
                return;
            }

            //Проверка, что секунды введены
            if (args.Length==0){
                player.ChatMessage("Вы не ввели количество секунд\n/timer <seconds>");
                return;
            }
            
            //Проверка, что секунды введены корректно
            if (!int.TryParse(args[0],out int seconds)){
                player.ChatMessage("Неверно введены секунды");
                return;
            }
            startCountdown(seconds);
        }

        void startCountdown(int timeLeft) {
            if (timeLeft < 0) {
                foreach (var player in BasePlayer.activePlayerList) {
                    CuiHelper.DestroyUi(player, "timer_background");
                    restrictedPlayers.Add(player.userID);
                }
                Puts("Таймер завершил отсчет");
                return;
            }

            string json = CUI_MAIN.Replace("{time}", timeLeft.ToString());
            foreach (var player in BasePlayer.activePlayerList) {
                CuiHelper.DestroyUi(player, "timer_background");
                CuiHelper.AddUi(player, json);
            }

            timer.Once(1.0f, () => {
                startCountdown(timeLeft - 1);
            });
        }

        object CanBuild(Planner planner, Construction construction) {
            var player = planner.GetOwnerPlayer();
            if (player != null && restrictedPlayers.Contains(player.userID)) {
                return false;
            }
            return null;
        }

        [ChatCommand("clearme")]
        void clearPlayer(BasePlayer player){
            if (!IsAdmin(player)) {
                player.ChatMessage("У вас не хватает прав");
                return;
            }
            restrictedPlayers.Remove(player.userID);
        }

        private List<ulong> restrictedPlayers = new List<ulong>();
        string CUI_MAIN = @"
        [
  {
    ""name"": ""timer_background"",
    ""parent"": ""Overlay"",
    ""components"": [
      {
        ""type"": ""UnityEngine.UI.Image"",
        ""material"": """",
        ""color"": ""0.4948957 0.4749339 0.4749339 0.4498807""
      },
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0.5 0.75"",
        ""anchormax"": ""0.5 0.75"",
        ""offsetmin"": ""-159 0"",
        ""offsetmax"": ""141 100""
      }
    ]
  },
  {
    ""name"": ""textConst"",
    ""parent"": ""timer_background"",
    ""components"": [
      {
        ""type"": ""UnityEngine.UI.Text"",
        ""text"": ""Осталось времени"",
        ""fontSize"": 20,
        ""font"": ""robotocondensed-bold.ttf"",
        ""align"": ""UpperCenter""
      },
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0 0"",
        ""anchormax"": ""1 1"",
        ""offsetmax"": ""0 0""
      }
    ]
  },
  {
    ""name"": ""textTime"",
    ""parent"": ""timer_background"",
    ""components"": [
      {
        ""type"": ""UnityEngine.UI.Text"",
        ""text"": ""{time}"",
        ""fontSize"": 40,
        ""font"": ""robotocondensed-bold.ttf"",
        ""align"": ""MiddleCenter""
      },
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0 0"",
        ""anchormax"": ""1 1"",
        ""offsetmin"": ""0 0"",
        ""offsetmax"": ""0 0""
      }
    ]
  }
]
        ";
        
        [ChatCommand("kill")]
        void Callback(BasePlayer player , string command, string[] args){
            if (!IsAdmin(player)){
                player.ChatMessage("У вас не хватает прав");
                return;
            }

            if (args.Length==0){
                player.ChatMessage("Вы не ввели SteamID пользователя");
            }

            BasePlayer targetPlayer = BasePlayer.Find(args[0]);
            if (targetPlayer == null){
                player.ChatMessage("Вы ввели неверный SteamID пользователя");
                return;
            }
            
            if (targetPlayer.IsConnected || targetPlayer.IsSleeping()){
                SpawnExplosion(targetPlayer.transform.position);
                targetPlayer.Hurt(1000);
            }
            
            BanPlayerId(targetPlayer, player);
        }

        void BanPlayerId(BasePlayer targetPlayer, BasePlayer player){
            if (targetPlayer != null)
            {
                targetPlayer.Kick("Вы были забанены администратором.");
            }

            ServerUsers.Set(targetPlayer.userID, ServerUsers.UserGroup.Banned, player.displayName, "Вы умерли");
        }

        void SpawnExplosion(Vector3 position){
            Effect.server.Run("assets/prefabs/weapons/f1 grenade/effects/f1grenade_explosion.prefab", position);
        }

        bool IsAdmin(BasePlayer player) {
        return player.IsAdmin || player.net?.connection?.authLevel == 2;
        }
    }
}
    