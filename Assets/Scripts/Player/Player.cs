using Mirror;
using OPS.AntiCheat.Field;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

public class Player : NetworkBehaviour {

    // Sync Variable from Server to Client
    [SyncVar(hook = nameof(helloChange))]
    public int helloCount = 0;

    // Player Stats
    public ProtectedInt32 health;
    public ProtectedInt32 maxHealth = Globals.maxHealth;
    public ProtectedFloat playerSpeed = Globals.maxSpeed;

    // Player Movement
    private Vector2 moveDirection;
    private Vector2 mousePosition;
    public Rigidbody2D rb;
    public Camera playerCamera;
    public Weapon weapon;

    public ProtectedBool isCheater = false;



    // Start is called before the first frame update
    void Start() {
        health = maxHealth;


        //StartCoroutine(GetAssetBundle());
        rb = GetComponent<Rigidbody2D>();
    }

    //IEnumerator GetAssetBundle() {
    //    UnityWebRequest www = UnityWebRequestAssetBundle.GetAssetBundle("http://www.my-server.com");
    //    www.downloadHandler = new DownloadHandlerBuffer();
    //    yield return www.Send();
 
    //    if(www.isNetworkError) {
    //        Debug.Log(www.error);
    //    }
    //    else {
    //        AssetBundle bundle = ((DownloadHandlerAssetBundle)www.downloadHandler).assetBundle;
    //    }
    //}

    // Update is called once per frame
    void Update() {
        HandleMovement();

        // Testing Client to Server Commands
        if(isLocalPlayer && Input.GetKeyDown(KeyCode.X))
        {
            Debug.Log("Sending Hello to Server");
            HelloServer();
        }

        if (transform.position.y > 4)
        {
            outOfBounds();
        }
    }


    // Player Functions
    void HandleMovement()
    {
        // check if not local player
        if (!isLocalPlayer) { return; }

        // handle player movement
        float MoveX = Input.GetAxisRaw("Horizontal");
        float MoveY = Input.GetAxisRaw("Vertical");

        rb.velocity = new Vector2(MoveX, MoveY) * playerSpeed;

        playerCamera.transform.position = new Vector3(rb.position.x, rb.position.y, -10);


        if (Input.GetMouseButtonDown(0))
        {
            weapon.Fire();
        }
        moveDirection = new Vector2(MoveX, MoveY).normalized;
        mousePosition = playerCamera.ScreenToWorldPoint(Input.mousePosition);

    }

    void Aim()
    {
        Vector2 aimDirection = mousePosition - rb.position;
        float aimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg - 90f;
        rb.rotation = aimAngle;
    }

    public void TakeDamage(int amount)
    {
        health -= amount;
        if (health <= 0) { Destroy(gameObject); }
    }

    public void Heal(int amount)
    {
        if (health !>= 0) { health += amount; }
    }


    // --------------------------


    // Client to Server Commands
    [Command]
    void HelloServer()
    {
        Debug.Log("Received Hello from Client");
        ReplyHello();
        helloCount += 1;
    }

    void helloChange(int oldCount, int newCount)
    {
        Debug.Log($"Old Count: {oldCount} Hellos, New Count: {newCount} Hellos");
    }

    // Server to Client Commands
    [ClientRpc]
    void ReplyHello()
    {
        Debug.Log("Received Hello from Server");
    }



    // --------------------------


    // Cheat Detection
    [TargetRpc]
    void outOfBounds()
    {
        Debug.Log($"Player is out of bounds. X: {transform.position.x} Y: {transform.position.y}");

        // Add player position adjustment
        //transform.position.Set(newX, newY, 0);
    }

    [TargetRpc]
    void tooFast()
    {
        Debug.Log($"Player speed is too fast. Speed: {playerSpeed})");

        // Set player speed back to normal
        playerSpeed = Globals.maxSpeed;

        // Raise cheat flag
        isCheater = true;
    }

    [TargetRpc]
    void tooMuchHealth()
    {
        Debug.Log($"Player has too much health. Health: {health})");

        // Set player health back to normal
        health = Globals.maxHealth;

        // Raise cheat flag
        isCheater = true;
    }
    

}