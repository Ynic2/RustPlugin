using System.ComponentModel;
using Facepunch.Extend;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins{
    [Info("Ynic's plugin","Ynic", "0.0.1")]
    class YnicsPlug : RustPlugin {
        [ChatCommand("timer")]
        void StartTimer(BasePlayer player, string command, string[] args) {
            //Проверка на админа
            if (!IsAdmin(player)) {
                player.ChatMessage("У вас не хватает прав");
                return;
            }
            
            if (!isWork){
              isWork = true;
            } else {
              player.ChatMessage("Таймер уже запущен");
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

            restrictedPlayers.Clear();
            StartCountdown(seconds);
        }

        void StartCountdown(int timeLeft) {
            if (timeLeft < 0) {
                foreach (var player in BasePlayer.activePlayerList) {
                    CuiHelper.DestroyUi(player, "timer_background");
                    restrictedPlayers.Add(player.userID);
                }
                Puts("Таймер завершил отсчет");
                isWork = false;
                return;
            }

            if (!isWork){
                return;
            }

            string json = CUI_MAIN.Replace("{time}", timeLeft.ToString());
            foreach (var player in BasePlayer.activePlayerList) {
                CuiHelper.DestroyUi(player, "timer_background");
                CuiHelper.AddUi(player, json);
            }
            
            timer.Once(1.0f, () => {
                if (!isWork){
                    return;
                }
                StartCountdown(timeLeft - 1);
            });
        }

        //Удаляем hud таймера, если он включен, иначе ничего не происходит
        [ChatCommand("stoptimer")]
        void StopTimer(){
            isWork = false;
            foreach (var player in BasePlayer.activePlayerList) {
                CuiHelper.DestroyUi(player, "timer_background");
                restrictedPlayers.Add(player.userID);
            }
        }

        object CanBuild(Planner planner, Construction construction) {
            var player = planner.GetOwnerPlayer();
            if (player != null && restrictedPlayers.Contains(player.userID)) {
                player.ChatMessage("Строительство запрещено");
                return false;
            }
            return null;
        }

        [ChatCommand("clearme")]
        void ClearPlayer(BasePlayer player){
            if (!IsAdmin(player)) {
                player.ChatMessage("У вас не хватает прав");
                return;
            }
            restrictedPlayers.Remove(player.userID);
        }

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
        
        void CreateMarker(Vector3 markPosition){
            MapMarkerGenericRadius mapMarker = GameManager.server.CreateEntity(
                    "assets/prefabs/tools/map/genericradiusmarker.prefab", markPosition) 
                    as MapMarkerGenericRadius;
            if (mapMarker == null) {
                Puts("Error: can't create mark");
                return;
            }
            mapMarker.alpha = 1f;
            mapMarker.color1 = Color.red;
            mapMarker.color2 = Color.black;
            mapMarker.radius = 0.1f;
            mapMarker.Spawn();
            mapMarker.SendUpdate();
            markOfToolbox.Add(mapMarker);
            Puts("Mark is succesfully created");
        }

        void OnEntityBuilt(Planner plan, GameObject go){
            BaseEntity entity = go.ToBaseEntity();
            if (entity == null){
                return;
            }

            if (entity.ShortPrefabName=="cupboard.tool.retro.deployed" 
            || entity.ShortPrefabName=="cupboard.tool.deployed"){
                Vector3 markPosition = entity.GetNetworkPosition();
                CreateMarker(markPosition);
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info){
            if (entity==null){
                return;
            }

            if (entity.ShortPrefabName=="cupboard.tool.retro.deployed" 
            || entity.ShortPrefabName=="cupboard.tool.deployed"){
                Vector3 entityPosition = entity.GetNetworkPosition();
                foreach (var mark in markOfToolbox){
                    if (mark.GetNetworkPosition() == entityPosition){
                        mark.Kill();
                        markOfToolbox.Remove(mark);
                        Puts("Toolbox was destoyed");
                    }
                }
            }
        }

        void OnPlayerConnected(BasePlayer player){
            if (player!=null){
                foreach(var mark in markOfToolbox){
                    Vector3 markPosition = mark.GetNetworkPosition();
                    mark.Kill();
                    markOfToolbox.Remove(mark);
                    CreateMarker(markPosition);
                }  
            }
        }

        private List<MapMarkerGenericRadius> markOfToolbox = new List<MapMarkerGenericRadius>();
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
        ""color"": ""0.4948957 0.4749339 0.4749339 0""
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
        bool isWork = false;
        
    }
        
}
    