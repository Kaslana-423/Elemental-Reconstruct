using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MonsterSpawner : MonoBehaviour
{
    GameObject TheMonster; //The current created monster
    Animator monsterAnimator; //The animator script of the monster
    public Text monsterNameText;

    public List<GameObject> AllMonsters = new List<GameObject>(); //a list of all monsters in the asset

    int CurrentMonster = 0;

    private void Start()
    {
        SummonNextPrevMonster(0);
    }

    public void SummonARandomMonster() //Summon a random monster from the asset
    {
        CurrentMonster = Random.Range(0, AllMonsters.Count);

        SummonNewMonster();
    }

    public void SummonNextPrevMonster(int Direction) //Summon either next or previous monster
    {
        CurrentMonster += Direction;
        if (CurrentMonster >= AllMonsters.Count)
            CurrentMonster = AllMonsters.Count - 1;
        else if (CurrentMonster < 0)
            CurrentMonster = 0;


        SummonNewMonster();
    }
       
    public void SummonNewMonster()
    {
        if (TheMonster != null)
            Destroy(TheMonster);

        //Create the selected monster
        TheMonster = Instantiate(AllMonsters[CurrentMonster], transform);

        //Special dimensions for certain monsters
        string MonsterName = TheMonster.name;
        float additioanlYPos = 0;
        float scalingFactor = 1;

        if (MonsterName.Contains("Shadow"))
        {
            scalingFactor = 0.65f;
        }

        //Change position and scale of the monster
        TheMonster.transform.position = new Vector2(transform.position.x, transform.position.y + additioanlYPos);
        TheMonster.transform.localScale = new Vector2(200, 200) * scalingFactor;

        monsterAnimator = TheMonster.GetComponent<Animator>();

        //string manipulation to get the name and id of the monster
        int idIndexStart = MonsterName.IndexOf('_', 1);
        int nameIndexStart = MonsterName.IndexOf('_', 9);
        int nameIndexEnd = MonsterName.IndexOf('(', 9);

        monsterNameText.text = "Monster "+ MonsterName.Substring (idIndexStart+1,nameIndexStart-idIndexStart-1)+ " : " +MonsterName.Substring(nameIndexStart + 1, nameIndexEnd - nameIndexStart -1);
    }

    public void ChangeAnimation(int AnimationID)  //Names are: 0=Idle, 1=Attack, 2=Hurt, 3=Walk, 4=Dead
    {
        if (monsterAnimator == null)
            return;

        //set the animation state to the selected one
        monsterAnimator.SetInteger("Animation", AnimationID);
    }

    public void RateUs()
    {
        System.Diagnostics.Process.Start("https://assetstore.unity.com/packages/slug/226712");
    }

}
