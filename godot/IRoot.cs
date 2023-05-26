using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

interface IRoot
{
    bool CanAdvanceToNextLevel();
    void AdvanceToNextLevel();

    void ReplayCurrentLevel();

    void BackToMainMenu();

    void ControllerSetup();

    void StartTutorial();

    void SolvePuzzles();

    void ShowCredits();

    void StartGame(SinglePlayerMenu.LevelToken token);

    void WatchReplay(string filepath);

    IServerConnection? GetServerConnection();
}
