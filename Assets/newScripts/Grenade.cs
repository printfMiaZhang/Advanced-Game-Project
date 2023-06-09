using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class Grenade : NetworkBehaviour {

    public Enemy enemyHealth;
    public Player player;

    private Vector2 targetPos;

    float timer = 0.5f;

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

        if (timer > 0) { timer -= Time.deltaTime; } else { Explode(); }

    }

        

    void Explode() {

        try
        {
            //Get the LandscapeSimulator in the screen
            LandscapeSimulator landscape = GameObject.Find("Landscape").GetComponent<LandscapeSimulator>();
            if (landscape != null)
            {
                //use the function from LandscapeSimulator.cs that allows it to burn the specific tile the bomb stops at
                landscape.BurnCellFromV2(new Vector2(transform.position.x - 0.5f, transform.position.y - 0.5f));
            }
        } catch (Exception e)
        {
            Debug.Log(e);
        }

        // Hurt Players and enemies



        //Destroy the object
        try
        {
            NetworkServer.Destroy(this.gameObject);
        }
        catch (Exception e)
        {
            Debug.Log("Grenade could not be destroyed over network. Destroying locally.\n" + e);
            Destroy(this.gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Enemy")
        {
            enemyHealth.TakeDamage(5);
            //Destroy(gameObject);
            Explode();
        }

        if (collision.gameObject.tag == "Player")
        {
            player.TakeDamage(5);
            //Destroy(gameObject);
            Explode();
        }
    }
}
