using System.Collections.Generic;
using FivePRS.Client.App;

namespace FivePRS.Client.Terminal.Apps
{
    public class CharactersApp : TerminalApp
    {
        public override string Id => "characters";

        public override string Label => "Characters";

        public override string Color => "#0d9488";

        public override string Icon => "M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8z M4 21a8 8 0 0 1 16 0";

        public override int Order => 30;

        public override string? Page => "nui/apps/characters/index.html";

        public override object BuildState() => CivilianPanel.BuildView();

        public override void OnAction(string action, IDictionary<string, object> data)
        {
            switch (action)
            {
                case "create":
                    CivilianPanel.CreateCharacter(data);
                    break;
                case "select":
                    CivilianPanel.SelectCharacter(data);
                    break;
                case "delete":
                    CivilianPanel.DeleteCharacter(data);
                    break;
                case "appearance":
                    CivilianPanel.RequestAppearance();
                    break;
                case "mugshot":
                    CivilianPanel.RequestMugshot(data);
                    break;
                case "discardMugshot":
                    CivilianPanel.DiscardMugshot();
                    break;
                case "photoUrl":
                    CivilianPanel.SetPhotoUrl(data);
                    break;
                case "removePhoto":
                    CivilianPanel.RemovePhoto(data);
                    break;
            }
        }
    }
}
