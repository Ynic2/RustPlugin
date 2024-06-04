using System.ComponentModel;
using Facepunch.Extend;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;
using Steamworks;
using System.Linq;

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
        void StopTimer(BasePlayer player){
            if (!IsAdmin(player)){
                player.ChatMessage("У вас не хватает прав");
                return;
            }
            isWork = false;
            foreach (var user in BasePlayer.activePlayerList) {
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
            if (construction!=null && 
            (construction.fullName.Basename()=="cupboard.tool.deployed.prefab" ||
            construction.fullName.Basename()=="cupboard.tool.retro.deployed.prefab")){
                foreach (var mark in markToOwner){
                    if (mark.Value.Contains(player.userID)){
                        player.ChatMessage("У вас уже стоит шкаф!");
                        return false;
                    }
                }
            }
            return null;
        }

        // private bool IsBuildingBlock(Construction construction)
        // {
        //     return construction.fullName.Contains("build");
        // }

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

            if (ulong.TryParse(args[0],out ulong steamID)){
                Killing(steamID, player);
            } else {
                player.ChatMessage("Неккоректно введен SteamID");
            }
            
        }

        void Killing(ulong userId, BasePlayer player = null){
            BasePlayer targetPlayer = BasePlayer.FindByID(userId);
            if (targetPlayer == null && player!=null){
                player.ChatMessage("Пользователь не найден");
                return;
            } else if(targetPlayer == null && player==null){
                return;
            }
            
            if (targetPlayer.IsConnected || targetPlayer.IsSleeping()){
                SpawnExplosion(targetPlayer.transform.position);
                targetPlayer.Hurt(1000);
            }

            if (player != null){
                BanPlayerId(targetPlayer, player.displayName);
                return;
            }

            BanPlayerId(targetPlayer);
        }
        
        void BanPlayerId(BasePlayer targetPlayer, string name = "Проигрыш"){
            if (targetPlayer != null)
            {
                targetPlayer.Kick("Вы были забанены администратором.");
            }

            ServerUsers.Set(targetPlayer.userID, ServerUsers.UserGroup.Banned, name, "Вы умерли");
        }

        void SpawnExplosion(Vector3 position){
            Effect.server.Run("assets/prefabs/weapons/f1 grenade/effects/f1grenade_explosion.prefab", position);
        }

        bool IsAdmin(BasePlayer player) {
            return player.IsAdmin || player.net?.connection?.authLevel == 2;
        }
        
        void CreateMarker(Vector3 markPosition, BasePlayer player, List<ulong> users = null){
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
            List<ulong> usersID = new List<ulong>();
            if (player != null){
                usersID = listFriend(player);
            } else {
                usersID = users;
            }
            markToOwner.Add(mapMarker, usersID);
            Puts("Mark is succesfully created");
        }

        void OnEntityBuilt(Planner plan, GameObject go){
            BaseEntity entity = go.ToBaseEntity();
            if (entity == null){
                return;
            }

            if (entity.ShortPrefabName=="cupboard.tool.retro.deployed" 
            || entity.ShortPrefabName=="cupboard.tool.deployed"){
                BasePlayer player = plan.GetOwnerPlayer();
                Vector3 markPosition = entity.GetNetworkPosition();
                CreateMarker(markPosition, player);
            }
        }

        object OnEntityKill(BaseNetworkable entity){
            if (entity!=null && (entity.ShortPrefabName=="cupboard.tool.retro.deployed" 
            || entity.ShortPrefabName=="cupboard.tool.deployed")){
                List<MapMarkerGenericRadius> markToRemove = new List<MapMarkerGenericRadius>();
                Vector3 entityPosition = entity.GetNetworkPosition();
                foreach (var mark in markToOwner){
                    if (mark.Key.GetNetworkPosition() == entityPosition){
                        markToRemove.Add(mark.Key);
                    }
                }
                foreach (var mark in markToRemove){
                    List<ulong> usersID = markToOwner[mark];
                    foreach (var userID in usersID){
                        removeEntitys(userID);
                        Killing(userID);
                    }
                    markToOwner.Remove(mark);
                    mark.Kill();
                    Puts("Toolbox was destoyed");
                }
            }
            return null;
        }

        void OnPlayerConnected(BasePlayer player){
            if (player!=null){
                Dictionary<Vector3, List<ulong>> saveMarks = new Dictionary<Vector3, List<ulong>>();
                foreach(var mark in markToOwner){
                    saveMarks.Add(mark.Key.GetNetworkPosition(), mark.Value);
                    mark.Key.Kill();
                }
                markToOwner.Clear();
                foreach(var mark in saveMarks){
                    CreateMarker(mark.Key,null,mark.Value);
                }
            }
        }

        [ChatCommand("pvp")]
        void ChangePVP(BasePlayer player){
            if (!IsAdmin(player)){
                player.ChatMessage("У вас не хватает прав");
                return;
            }
            pvpMode = !pvpMode;
            player.ChatMessage("Pvp = " + pvpMode);
            Puts("Pvp = " + pvpMode);
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {   
            if (!pvpMode){
                if (entity is BasePlayer targetPlayer && 
                !targetPlayer.ShortPrefabName.Contains("npc")){

                    if (info.Initiator is BasePlayer attackerPlayer && 
                    !attackerPlayer.ShortPrefabName.Contains("npc")){
                        if (attackerPlayer != targetPlayer)
                        {
                            return true;
                        }
                    }
                }
            }
            return null;
        }

        [ChatCommand("checkTool")]
        void CheckOfToolbox(BasePlayer player){
            if (!IsAdmin(player)){
                player.ChatMessage("У вас не хватает прав");
                return;
            }
            string allPosition = "Шкафы на координатах: ";
            foreach (var mark in markToOwner){
                allPosition += "\""+mark.Key.GetNetworkPosition().ToString();
                foreach (var id in mark.Value)
                {
                    allPosition += " " + id.ToString();
                }
                allPosition += "\"";
            }
            player.ChatMessage(allPosition);
        }

        [ChatCommand("friends")]
        void CheckFriends(BasePlayer player, string command, string[] args){
            if (!IsAdmin(player)){
                player.ChatMessage("Не хватает прав!");
                return;
            }

            if (args.Length==0){
                player.ChatMessage("Введите id пользователя");
                return;
            }
            
            BasePlayer targertPlayer = BasePlayer.Find(args[0]);

            List<ulong> usersID = listFriend(targertPlayer);

            foreach (var id in usersID){
                player.ChatMessage(id.ToString());
            }
        }

        List<ulong> listFriend(BasePlayer player){
            RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindPlayersTeam(player.userID);

            if (team == null)
            {
                Puts("NO TEAM");
                List<ulong> userId = new List<ulong>();
                userId.Add(player.userID);
                return userId;
            } 

            List<ulong> usersId = new List<ulong>();
            return team.members;
        }

        void removeEntitys(ulong userID){
            foreach (var entity in BaseNetworkable.serverEntities){
                if (entity is BuildingBlock block){
                    if (block.OwnerID == userID){
                        block.Kill();
                    }
                }
            }
        }

        private Dictionary<MapMarkerGenericRadius, List<ulong>> markToOwner = new Dictionary<MapMarkerGenericRadius, List<ulong>>();
        
        object OnTeamUpdate(ulong currentTeam, ulong newTeam, BasePlayer player)
        {
            RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindPlayersTeam(player.userID);
            ulong teamLeader = team.GetLeader().userID;
            MapMarkerGenericRadius key = null;
            foreach(var mark in markToOwner){
                if (mark.Value.Contains(teamLeader)){
                    key = mark.Key;
                }
            }
            if (key!=null){
                foreach(var member in team.members){
                    if (!markToOwner[key].Contains(member)){
                        markToOwner[key].Add(member);
                    }
                }
            }
            else{
                foreach(var mark in markToOwner){
                    foreach(var member in team.members){
                        if (mark.Value.Contains(member)){
                            key=mark.Key;
                        }
                    }
                }
                if (key==null){
                    return null;
                } else {
                    foreach(var member in team.members){
                        if(!markToOwner[key].Contains(member)){
                            markToOwner[key].Add(member);
                        }
                    }
                }
            }
            return null;
        }

        private bool pvpMode = true;
        private List<ulong> restrictedPlayers = new List<ulong>();
        private string CUI_MAIN = @"
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
    