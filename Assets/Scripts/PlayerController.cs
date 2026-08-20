using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;

public class PlayerController : MonoBehaviour
{   
    [Header ("Horizontal Movement Settings")]
    [SerializeField] private float walkSpeed = 20;
    [Space(5)]

    [Header ("Vertical Movement Settings")]
    [SerializeField] private float jumpForce = 20;
    float jumpBufferCounter = 0;
    [SerializeField] private float jumpBufferFrames = 8;
    float coyoteTimeCounter = 0;
    [SerializeField] private float coyoteTime; 
    private int airJumpCounter = 0;
    [SerializeField] private int maxAirJumps;
    [Space(5)]

    [Header("GroundCheck Settings")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckY = 0.2f;
    [SerializeField] private float groundCheckX = 0.5f;
    [SerializeField] private LayerMask whatIsGround;
    [Space(5)]

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed;
    [SerializeField] private float dashTime;
    [SerializeField] private float dashCooldown;
    [Space(5)]

    [Header("Attack Settings")]
    [SerializeField] Transform SideAttackTransform; 
    [SerializeField] Transform UpAttackTransform;
    [SerializeField] Transform DownAttackTransform;
    [SerializeField] Vector2 SideAttackArea, UpAttackArea, DownAttackArea;
    [SerializeField] LayerMask attackableLayer;
    [SerializeField] private float timeBetweenAttack;
    private float timeSinceAttack;
    [SerializeField] float damage;
    [SerializeField] GameObject slashEffect;
    [Space(5)]

    [Header("Recoil Settings")]
    [SerializeField] private float recoilXSteps = 5;
    [SerializeField] private float recoilYSteps = 5;
    [SerializeField] private float recoilXSpeed = 100;
    [SerializeField] private float recoilYSpeed = 100;
    [SerializeField] private float stepsXRecoiled, stepsYRecoiled;
    [Space(5)]

    [Header("Health Settings")]
    [SerializeField] public int health;
    [SerializeField] public int maxHealth;
    [Space(5)]

    bool attack = false;
    bool attack2 = false;

    [HideInInspector] public PlayerStateList pState;
    private Rigidbody2D rb;
    private float xAxis, yAxis;
    private float gravity;
    Animator anim;
    private bool canDash = true;
    private bool dashed = false;

    public static PlayerController Instance;

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pState = GetComponent<PlayerStateList>();

        rb = GetComponent<Rigidbody2D>();

        anim = GetComponent<Animator>();

        gravity = rb.gravityScale;

        health = maxHealth;


    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(SideAttackTransform.position, SideAttackArea);
        Gizmos.DrawWireCube(UpAttackTransform.position, UpAttackArea);
        Gizmos.DrawWireCube(DownAttackTransform.position, DownAttackArea);
    }

    // Update is called once per frame
    void Update()
    {
        GetInputs();
        UpdateJumpVariables();

        Attack();
        if (pState.dashing) return;
        Flip();
        Move();
        Jump();
        StartDash();
        Recoil();
    }

    void GetInputs()
    {
        xAxis = Input.GetAxisRaw("Horizontal");
        yAxis = Input.GetAxisRaw("Vertical");
        attack = Input.GetMouseButtonDown(0);
        attack2 = Input.GetButtonDown("Attack");
    }


    // Turns the player around depending which side you move to
    void Flip()
    {
        if(xAxis < 0)
        {
            transform.localScale = new Vector2(-3, transform.localScale.y);
            pState.lookingRight = false;
        }
        else if(xAxis > 0)
        {
            transform.localScale = new Vector2(3, transform.localScale.y);
            pState.lookingRight = true;
        }
    }

    // Makes the player move.. Duh
    private void Move()
    {
        rb.linearVelocity = new Vector2(walkSpeed * xAxis, rb.linearVelocity.y);
        anim.SetBool("Running", rb.linearVelocity.x != 0 && Grounded());
    }

    // Makes the player Dash
    void StartDash()
    {
        if(Input.GetButtonDown("Dash") && canDash && !dashed)
        {
            StartCoroutine(Dash());
            dashed = true;
        }

        if(Grounded())
        {
            dashed = false;
        }
    }

    // Dash Functionality
    IEnumerator Dash()
    {   
        canDash = false;
        pState.dashing = true;
        anim.SetTrigger("Dash");
        anim.SetBool("Jumping", false);
        rb.gravityScale = 0;
        rb.linearVelocity = new Vector2(transform.localScale.x * dashSpeed, 0);
        yield return new WaitForSeconds(dashTime);
        rb.gravityScale = gravity;
        pState.dashing = false;
        if (!Grounded())
        {
            anim.SetBool("Falling", true);
        }
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    void Attack()
    {
        timeSinceAttack += Time.deltaTime;
        if((attack || attack2) && timeSinceAttack >= timeBetweenAttack)
        {
            timeSinceAttack = 0;

            if(yAxis == 0 || yAxis < 0 && Grounded())
            {
                anim.SetTrigger("Attack");
                Hit(SideAttackTransform, SideAttackArea, ref pState.recoilingX, recoilXSpeed);
                //Instantiate(slashEffect, SideAttackTransform);
            }
            else if(yAxis>0)
            {
                anim.SetTrigger("UpAttack");
                Hit(UpAttackTransform, UpAttackArea, ref pState.recoilingY, recoilYSpeed);
                //SlashEffectAtAngle(slashEffect, 90, UpAttackTransform);
            }
            else if(yAxis<0 && !Grounded())
            {
                anim.SetTrigger("Attack");
                Hit(DownAttackTransform, DownAttackArea, ref pState.recoilingY, recoilYSpeed);
                //SlashEffectAtAngle(slashEffect, -90, DownAttackTransform);
            }
        }
    }

    void Hit(Transform _attackTransform, Vector2 _attackArea, ref bool _recoilDir, float _recoilStrength)
    {
        Collider2D[] objectsToHit = Physics2D.OverlapBoxAll(_attackTransform.position, _attackArea, 0, attackableLayer);
        List<Enemy> hitEnemies = new List<Enemy>();

        if (objectsToHit.Length > 0)
        {
            _recoilDir = true;
            Debug.Log("Hit");
        }
        for (int i = 0; i < objectsToHit.Length; i++)
        {
            Enemy e = objectsToHit[i].GetComponent<Enemy>();
            if (e && !hitEnemies.Contains(e))
            {
                e.EnemyHit(damage, (transform.position - objectsToHit[i].transform.position).normalized, _recoilStrength);
                hitEnemies.Add(e);
            }
        }
    }

    //void SlashEffectAtAngle(GameObject _slashEffect, int _effectAngle, Transform _attackTransform)
    //{
    //    _slashEffect = Instantiate(_slashEffect, _attackTransform);
    //    _slashEffect.transform.eulerAngles = new Vector3(0, 0, _effectAngle);
    //    _slashEffect.transform.localScale = new Vector2(transform.localScale.x, transform.localScale.y);
    //}

    void Recoil()
    {
        if (pState.recoilingX)
        {
            if (pState.lookingRight)
            {
                rb.linearVelocity = new Vector2(-recoilXSpeed, 0);
            } 
            else
            {
                rb.linearVelocity = new Vector2(recoilXSpeed, 0);
            }
        }

        if (pState.recoilingY)
        {
            rb.gravityScale = 0;
            if (yAxis < 0)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, recoilYSpeed);
            }
            else
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -recoilYSpeed);
            }
            airJumpCounter = 0;
        }
        else
        {
            rb.gravityScale = gravity;
        }

        //stop Recoil
        if (pState.recoilingX)
        {
            stepsXRecoiled+=Time.deltaTime;
            if(stepsXRecoiled >= recoilXSteps)
            {
                StopRecoilX();
            }
        }
        if (pState.recoilingY && stepsYRecoiled < recoilYSteps)
        {
            stepsYRecoiled += Time.deltaTime;
            if (stepsYRecoiled >= recoilYSteps)
            {
                StopRecoilY();
            }
        }

        if (Grounded())
        {
            StopRecoilY();
        }
    }
    void StopRecoilX()
    {
        stepsXRecoiled = 0;
        pState.recoilingX = false;
    }
    void StopRecoilY()
    {
        stepsYRecoiled = 0;
        pState.recoilingY = false;
    }

    public void TakeDamage(float _damage)
    {
        health -= Mathf.RoundToInt(_damage);
        StartCoroutine(StopTakingDamage());
    }

    IEnumerator StopTakingDamage()
    {
        pState.invincible = true;
        anim.SetTrigger("takeDamage");
        ClampHealth();
        yield return new WaitForSeconds(1f);
        pState.invincible = false;
    }

    void ClampHealth()
    {
        health = Mathf.Clamp(health, 0, maxHealth);
    }

    // Checks if player is on the ground
    public bool Grounded()
    {
        if (Physics2D.Raycast(groundCheckPoint.position, Vector2.down, groundCheckY, whatIsGround) 
        || Physics2D.Raycast(groundCheckPoint.position + new Vector3(groundCheckX, 0, 0), Vector2.down, groundCheckY, whatIsGround)
        || Physics2D.Raycast(groundCheckPoint.position + new Vector3(-groundCheckX, 0, 0), Vector2.down, groundCheckY, whatIsGround))
        {
            return true;
        }
        else
        {
            return false;
        }
    }


    // Makes player Jump after checking a few dependencies
    void Jump()
    {   
        if(Input.GetButtonDown("Jump") && rb.linearVelocity.y > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);

            pState.jumping = false;
        }
            if (!pState.jumping)
            {
                if (jumpBufferCounter > 0 && coyoteTimeCounter > 0)
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce);

                    pState.jumping = true;
                }
                else if (!Grounded() && airJumpCounter < maxAirJumps && Input.GetButtonDown("Jump"))
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce);

                    pState.jumping = true;

                    airJumpCounter++;
                }
            }
        anim.SetBool("Jumping", !Grounded());
        anim.SetBool("Falling", rb.linearVelocity.y < 0 && !Grounded() && !pState.dashing);
    }
    

    // Resets Jumping variables if on the Ground
    // Changes them over time if airborne
    void UpdateJumpVariables()
    {
        if (Grounded())
        {
            pState.jumping = false;
            coyoteTimeCounter = coyoteTime;
            airJumpCounter = 0;
            anim.SetBool("Falling", false);
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if(Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferFrames;
        }
        else
        {
            jumpBufferCounter = jumpBufferCounter - Time.deltaTime * 10;
        }
    }
}
