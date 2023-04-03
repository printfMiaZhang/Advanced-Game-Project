using Mirror;
using OPS.AntiCheat.Field;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Enemy : NetworkBehaviour {
    public static event Action<Enemy> OnEnemyKilled;
    [Header("Initial Data")]
    [SerializeField] ProtectedInt32 health, maxHealth = 4;

    //movement data
    [SerializeField] ProtectedFloat moveSpeed = 3;
    [SerializeField] ProtectedFloat regularSpeed = 3;
    [SerializeField] ProtectedFloat chargeSpeed = 5;
    private Rigidbody2D rb;

    [Header("Attack Data")]
    [SerializeField] ProtectedInt32 LevelOfDamage;

    //List of players close to the enemy
    [SerializeField]public readonly HashSet<GameObject> playersDetected = new HashSet<GameObject>();
    //List of fellow enemies nearby
    [SerializeField]public readonly HashSet<GameObject> comradesNearby = new HashSet<GameObject>();

    //Player that is being targeted
    [SerializeField] private GameObject playerTarget = null;
    //Player's script (to track health)
    [SerializeField] private Player playerScript = null;

    //Patrol data
    private Vector3 spawnPosition;

    //Data for enemy movement
    private Vector3 target;
    private Vector2 moveDirection;

    [Header("Damage-related Data")]
    //Health Damage Buffer... doesn't actually buffer damage just for showing visual damage
    [SerializeField] ProtectedFloat dmgBufferInterval = 1;
    ProtectedFloat dmgBuffer;
    //Interaction with the fire
    [SerializeField] ProtectedFloat fireCheckInterval = 1;
    ProtectedFloat fireCheck;
    private LandscapeSimulator landscape;

    private PlayerEnemyTracker playerEnemyTracker;

    //Player Sprite
    [SerializeField] SpriteRenderer enemySprite;
    [SerializeField] Animator enemyAnimator;

    private void Awake() {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start() {
        health = maxHealth;
        
        spawnPosition = transform.position;

        // perform some random initialization on the flanking behaviour
        flankPatienceTimer = UnityEngine.Random.Range(flankingPatience/2, flankingPatience);
        if (UnityEngine.Random.Range(0, 2) == 1) {
            flankingAngleInterval *= -1;
        }

        landscape = GameObject.Find("Landscape").GetComponent<LandscapeSimulator>();

        playerEnemyTracker = GameObject.Find("CharacterTracker").GetComponent<PlayerEnemyTracker>();
        if (playerEnemyTracker != null ) {
            playerEnemyTracker.enemies.Add(gameObject);
        }
    }

    private void Update() {
        //HandleMovement();
        AIBehaviour();
        //Movetowards behaviour
        if (moveTo && !reachedLoc) {
            MoveToBehaviour();
        }
        //Damage related behaviour
        if (dmgBuffer > 0) {
            dmgBuffer -= Time.deltaTime;
            enemySprite.color = new Color(1, 1 - dmgBuffer, 1 - dmgBuffer);
        } else {
            enemySprite.color = Color.white;
        }
        if (landscape == null) return;
        if (fireCheck > 0) {
            fireCheck -= Time.deltaTime;
        } else {
            Vector3Int tileLoc = new Vector3Int((int)Mathf.Round(rb.transform.position.x - 0.5f), (int)Mathf.Round(rb.transform.position.y - 0.5f), 0);

            if (landscape.FireGrid.GetTile(tileLoc) != null) {
                TakeDamage(2);
            }
            fireCheck = fireCheckInterval;
        }
    }
    [Header("AI States")]
    public ProtectedInt32 behaviourState = 0; //incurred to direct whether to remain idle or to Attack the nearest player
    // 0 = idle
    static ProtectedInt32 idle = 0;
    // 1 = attack
    static ProtectedInt32 attack = 1;

    public ProtectedInt32 attackState; //incurred only when behaviourState = attack
    // 0 = confront
    static ProtectedInt32 confront = 0;
    // 1 = flank
    static ProtectedInt32 flank = 1;
    // 2 = charge
    static ProtectedInt32 charge = 2;
    // 3 = quick retreat
    static ProtectedInt32 retreat = 3;

    //Determine which behaviour the player should explore
    private void AIBehaviour() {
        if (behaviourState == idle) {
            IdleBehaviour();
        } else if (behaviourState == attack) {
            AttackBehaviour();
        }
    }
    //******************************************* Idle Behaviour *******************************************
    [Header("Idle Patrol Data")]
    [SerializeField] ProtectedFloat patrolRadius;
    [SerializeField]ProtectedFloat waitIdlyTime = 2;
    ProtectedFloat idleTimer;
    void IdleBehaviour() {
        if (!moveTo) {
            if (idleTimer > 0) {
                idleTimer -= Time.deltaTime;
            } else {
                MoveTo(FindRandomPointInRadius(spawnPosition, patrolRadius));
                idleTimer = waitIdlyTime;
                moveSpeed = regularSpeed;
            }
        }
        playerTarget = DetectNearestPlayerInRadius();
        if (playerTarget != null) {
            TriggerAttackMode(playerTarget);
        }
    }

    Vector3 FindRandomPointInRadius(Vector3 center, float radius) {
        Vector3 point = new Vector3(center.x + UnityEngine.Random.Range(-radius, radius), center.y + UnityEngine.Random.Range(-radius, radius), 0);
        return point;
    }

    public void TriggerAttackMode(GameObject player) {
        if (behaviourState == 1) return;
        playerTarget = player;
        playerScript = playerTarget.GetComponent<Player>();
        behaviourState = 1;
        attackState = confront;
        AlertFriends();
    }

    void AlertFriends() {
        foreach (GameObject friend in comradesNearby) {
            if (friend != null) {
                friend.GetComponent<Enemy>().TriggerAttackMode(gameObject);
            } else {
                comradesNearby.Remove(friend);
            }
        }
    }
    //******************************************* Attack Behaviour *******************************************
    void AttackBehaviour() {
        if (playerTarget == null || playerScript == null) {
            behaviourState = 0;
            playerTarget = DetectNearestPlayerInRadius();
            if (playerTarget != null) {
                TriggerAttackMode(playerTarget);
            } else {
                return;
            }
        } else {
            //Set behaviour based on attack mode
            if (attackState == confront)
                ConfrontPhase();
            else if (attackState == flank)
                FlankPhase();
            else if (attackState == charge)
                ChargePhase();
            else if (attackState == retreat)
                RetreatPhase();
        }
    }
    //******************************************* Attack Phases *******************************************
    [Header("Confrontation Data")]
    //Confront phase: Try to get within a certain radius of the player before shifting to flank phase
    [SerializeField] ProtectedFloat minRadius = 3;
    [SerializeField] ProtectedFloat radiusTolerance = 1;
    void EnterConfrontPhase() {
        moveSpeed = regularSpeed;
        MoveTo(transform.position);
        attackState = confront;
    }
    void ConfrontPhase() {
        MoveTo(playerScript.rb.transform.position);
        if (moveTo) {
            float dist = Vector3.Distance(playerScript.rb.transform.position, transform.position);
            if (dist <= minRadius) {
                Debug.Log("Player reached");
                EnterFlankPhase();
            }
        }
    }

    [Header("Flanking Data")]
    // Flank Phase: the enemy will try to circle around the target player before charging at them
    [SerializeField] ProtectedFloat flankingPatience = 6; //Highest level of patience to attempt flanking the player before attacking
    [SerializeField] ProtectedFloat flankingAngleInterval = 15; //floating point value in degrees denoting how many angles are used to loop around the player.
                                                                //Smaller values are smoother but have the AI calculate the rotation more frequently
    ProtectedFloat flankPatienceTimer;
    void EnterFlankPhase() {
        Debug.Log("begin Flank");
        attackState = flank;
        MoveTo(transform.position);
        moveSpeed = regularSpeed;
    }
    void FlankPhase() {
        //check if the enemy has any patience, otherwise perform a charge
        if (flankPatienceTimer > 0) {
            //decrement timer
            flankPatienceTimer -= Time.deltaTime;
            //continue flanking if done moving
            if (!moveTo) {
                //find a new vector to move to that is a rotation of the current position around the player. Mimics a flanking behaviour
                MoveTo(NextRotationAroundPoint(playerScript.rb.transform.position, transform.position, flankingAngleInterval));
            }
            float dist = Vector3.Distance(playerScript.rb.transform.position, transform.position);
            if (dist < minRadius - radiusTolerance) {
                //EnterChargePhase();
            } else if (dist > minRadius + radiusTolerance) {
                //EnterConfrontPhase();
            }
        } else {
            // If patience expires, move to charge phase
            EnterChargePhase();
        }
    }
    Vector3 NextRotationAroundPoint(Vector3 pivotPoint, Vector3 position, float angle) {
        //get a matrix-estimated value of the angle to define theta
        float theta = (angle / 180) * 3.1415f;
        //generate a two-dimensional rotation around the z-axis
        float x = ((position.x - pivotPoint.x) * MathF.Cos(theta)) - ((position.y - pivotPoint.y) * Mathf.Sin(theta));
        float y = ((position.x - pivotPoint.x) * Mathf.Sin(theta)) + ((position.y - pivotPoint.y) * Mathf.Cos(theta));
        //offset rotated vector by the pivot point
        x += pivotPoint.x;
        y += pivotPoint.y;

        return new Vector3(x, y, 0);
    }

    [Header("Charging Data")]
    //Charge Phase: Enemy will charge at the player until they run out of adrenaline or manage to attempt an attack
    [SerializeField] ProtectedFloat maxAdrenalineLevel = 10;
    [SerializeField] ProtectedFloat closingDistance = 1.5f;
    [SerializeField] ProtectedFloat inflictDamageDelay = 0.1f;
    ProtectedFloat adrenalineTimer = 0;
    ProtectedBool enteredAttack = false;
    ProtectedFloat timeTillDamage = 0;
    void EnterChargePhase() {
        Debug.Log("Begin Charge");
        MoveTo(transform.position);
        attackState = charge;
        moveSpeed = chargeSpeed;
        // redefine flanking behaviour to a random new level of patience and direction to flank around the player
        flankPatienceTimer = UnityEngine.Random.Range(flankingPatience/2, flankingPatience);
        if (UnityEngine.Random.Range(0, 2) == 1) {
            flankingAngleInterval *= -1;
        }
        //Initialize adrenaline level
        adrenalineTimer = maxAdrenalineLevel;
    }
    void ChargePhase() {
        float dist = Vector3.Distance(playerScript.rb.transform.position, transform.position);
        if (dist <= closingDistance && !enteredAttack) {
            MoveTo(transform.position);
            if (timeTillDamage <= 0) {
                enemyAnimator.ResetTrigger("Attack");
                enemyAnimator.SetTrigger("Attack");
            }
            enteredAttack = true;
            timeTillDamage = inflictDamageDelay;
        } else {
            MoveTo(playerScript.rb.transform.position);
        }
        //behaviour for if in attack
        if (enteredAttack) {
            moveTo = false;
            timeTillDamage -= Time.deltaTime;
            if (timeTillDamage <= 0) {
                if (dist <= closingDistance) {
                    playerScript.TakeDamage()
                }
            }
        }
    }

    //[Header("Retreat Data")]
    [SerializeField] ProtectedFloat maxRetreatDistance = 5;
    void RetreatPhase() {

    }

    //******************************************* Movement Behaviour *******************************************
    //allows for very simple moveto command to inform the entity to go somewhere
    //is the enemy currently supposed to be moving somewhere?
    ProtectedBool moveTo;
    //Has the enemy reached their location?
    ProtectedBool reachedLoc;
    public void MoveTo(Vector3 newTarget) {
        moveTo = true;
        target = newTarget;
        reachedLoc = false;
    }

    [Header("MoveTo Data")]
    [SerializeField] ProtectedFloat MoveToTolerance = 0.05f;
    Vector3 lastFrameLoc;
    ProtectedInt32 stuckWarnings = 0;
    private void MoveToBehaviour() {
        if (Vector3.Distance(target, rb.transform.position) < MoveToTolerance) {
            moveTo = false;
            reachedLoc = true;
            rb.velocity = Vector3.zero;
            enemyAnimator.SetBool("Walking", false);
            return;
        } else if (rb.transform.position == lastFrameLoc) {
            if (stuckWarnings >= 100) {
                Debug.Log("Bad Path, ending moveto");
                stuckWarnings = 0;
                moveTo = false;
                reachedLoc = false;
                rb.velocity = Vector3.zero;
                enemyAnimator.SetBool("Walking", false);
                return;
            } else {
                if (Time.timeScale > 0) stuckWarnings++;
            }
        } else {
            stuckWarnings = 0;
        }
        Vector3 direction = (target - transform.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        rb.MoveRotation(angle);
        moveDirection = direction;
        rb.velocity = new Vector2(moveDirection.x, moveDirection.y) * moveSpeed;
        enemyAnimator.SetBool("Walking", true);
    }

    //Search through the hashset to try and find the nearest player that has entered the enemy's radius
    GameObject DetectNearestPlayerInRadius() {
        if (playersDetected.Count == 0) return null;
        GameObject nearestPlayer = playersDetected.ElementAt(0);
        foreach (GameObject player in playersDetected) {
            float newDistance = Vector3.Distance(transform.position, player.transform.position);
            float currentDistance = Vector3.Distance(transform.position, nearestPlayer.transform.position);
            if (newDistance < currentDistance) {
                nearestPlayer = player;
            }
        }
        return nearestPlayer;
    }

    bool IsPlayerValid(GameObject playerObj, Player playerScr) {
        if (playerScr == null || playerObj == null) {
            return false;
        } else {
            if (playerScr.health <= 0) {
                return false;
            } else {
                return true;
            }
        }
    }

    public void TakeDamage(int damageAmount) {
        health -= damageAmount;

        dmgBuffer = dmgBufferInterval;

        if (health <= 0) {
            Destroy(gameObject);
            OnEnemyKilled?.Invoke(this);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision) {
        Player player = collision.gameObject.GetComponent<Player>();
        if (player != null) {
            playersDetected.Add(player.gameObject);
        }
        Enemy enemy = collision.gameObject.GetComponent<Enemy>();
        if (enemy != null) {
            comradesNearby.Add(enemy.gameObject);
        }
    }
    private void OnTriggerExit2D(Collider2D collision) {
        Player player = collision.gameObject.GetComponent<Player>();
        if (player != null) {
            playersDetected.Remove(player.gameObject);
        }
        Enemy enemy = collision.gameObject.GetComponent<Enemy>();
        if (enemy != null) {
            comradesNearby.Remove(enemy.gameObject);
        }
    }
}
