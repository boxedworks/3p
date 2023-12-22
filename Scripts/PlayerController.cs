using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.InputSystem;

using Mirror;

public class PlayerController : GameEntity
{

  Vector3 _aimPos, _aimDir;

  // Start is called before the first frame update
  void Start()
  {
    s_Players.Add(this);
    s_PlayersMapped.Add(transform.GetChild(1).gameObject.GetInstanceID(), this);
    s_PlayersNetMapped.Add(netId, this);

    //
    if (isLocalPlayer)
    {
      _physicsData = new();
      _cameraData = new();

      _skillData = new(this);

      _healthData = new(GameObject.Find("PlayerHealth").GetComponent<UnityEngine.UI.Slider>(), 100f)
      {
        _Text = GameObject.Find("PlayerHealthText").GetComponent<TMPro.TextMeshProUGUI>()
      };
      _healthData.UpdateUI();
    }
    else
    {
      _healthData = new(null, 100f);
    }
  }

  //
  SkillData _skillData;
  class SkillData
  {

    PlayerController _player;

    //
    SkillBase[] _skills;
    public SkillData(PlayerController player)
    {
      _player = player;

      //
      _skills = new SkillBase[4];
      new Skill_Firebolt(this);
      new SkillBase(this);
      new SkillBase(this);
      new SkillBase(this);

    }

    //
    public void Update(Gamepad gamepad)
    {
      foreach (var skill in _skills)
        skill.Update();

      //
      if (gamepad.leftTrigger.wasPressedThisFrame)
      {
        _skills[0].Use();
      }
    }

    // Holds individual skill infos
    class SkillBase
    {

      //
      PlayerController _player;

      // UI infos
      Transform transform;

      Transform _loadScreen;
      protected float _loadTimer, _loadDuration;

      protected int _count, _countMax;
      TMPro.TextMeshProUGUI _countText;

      protected float _useTime, _useRate;

      //
      public bool _CanUse { get { return _count > 0 && Time.time - _useTime >= _useRate; } }

      //
      public SkillBase(SkillData skillData)
      {
        _player = skillData._player;

        //
        var i = 0;
        for (; i < 4; i++)
          if (skillData._skills[i] == null)
          {
            skillData._skills[i] = this;
            break;
          }

        // Gather main component
        transform = GameObject.Find("PlayerSkills").transform.GetChild(0).GetChild(i);

        // Set icon
        //transform.GetChild(0).GetComponent<UnityEngine.UI.Image>();

        // Gather loading screen
        _loadScreen = transform.GetChild(1);
        _loadDuration = Random.Range(0.3f, 3f);

        // Set count
        _countText = transform.GetChild(3).GetChild(0).GetComponent<TMPro.TextMeshProUGUI>();
        _countMax = Random.Range(5, 20);
      }

      //
      public void Update()
      {
        var loadScale = _loadScreen.localScale;
        var loadTime = Time.time - _loadTimer;
        if (_count < _countMax)
        {
          if (loadTime >= _loadDuration)
          {
            var loadDiff = loadTime - _loadDuration;
            _loadTimer = Time.time + loadDiff;
            loadTime = Time.time - _loadTimer;

            _count++;
            UpdateCountText();
          }
        }
        else
          loadTime = 0f;
        loadScale.y = loadTime / _loadDuration;
        _loadScreen.localScale = loadScale;
      }

      //
      protected void UpdateCountText()
      {
        _countText.text = $"{_count}";
      }

      //
      public void Use(int amount = 1)
      {
        if (!_CanUse || amount == 0) return;
        _useTime = Time.time;

        if (_count == _countMax)
          _loadTimer = Time.time;
        _count = Mathf.Clamp(_count - amount, 0, _countMax);
        UpdateCountText();

        ProjectileScript.SpawnProjectile(new ProjectileScript.ProjectileSpawnData()
        {
          Source = _player,
          ProjectileType = ProjectileScript.ProjectileType.FIREBALL,

          SpawnPosition = _player._aimPos,
          Direction = _player._aimDir,
          Speed = 4000f,
          Size = 0.7f,

          Damage = 50f,
        });
      }
    }

    //
    class Skill_Firebolt : SkillBase
    {
      public Skill_Firebolt(SkillData skillData) : base(skillData)
      {
        _countMax = _count = 1;
        UpdateCountText();

        _loadDuration = 0.5f;
        _useRate = 0.25f;
      }
    }
  }

  //
  HealthData _healthData;
  class HealthData
  {
    public float _Health, _HealthMax;

    public UnityEngine.UI.Slider _Slider;
    public TMPro.TextMeshProUGUI _Text;

    public HealthData(UnityEngine.UI.Slider slider, float healthMax)
    {
      _HealthMax = _Health = healthMax;
      _Slider = slider;
    }

    public void UpdateUI()
    {
      if (_Slider != null)
        _Slider.value = _Health / _HealthMax;
      if (_Text != null)
        _Text.text = $"{_Health}/{_HealthMax}";
    }
  }

  //
  PhysicsData _physicsData;
  class PhysicsData
  {
    public Vector3 Velocity, ExternalForce;

    public bool isAirborn = true;
    public float AirbornTime;
    public float AirbornForce;
  }

  // Update is called once per frame
  void Update()
  {

    if (!isLocalPlayer) return;

    if (isServer)
    {
      PlayerController.UpdateIncr();
      EnemyScript.UpdateIncr();
    }

    // Gather controller input
    var gamepad = Gamepad.current;

    // Update skills
    _skillData.Update(gamepad);

    // Basic input
    var input0 = Vector2.zero;
    var input1 = Vector2.zero;
    input0 = gamepad.leftStick.value;
    input1 = gamepad.rightStick.value;

    var camera = Camera.main;
    var cameraForward = camera.transform.forward;
    cameraForward.y = 0f;
    cameraForward = cameraForward.normalized;

    var dt = Mathf.Clamp(Time.deltaTime, 0f, 0.1f);

    // Apply gravity / airborn force
    var gravityForce = -17f;
    _physicsData.AirbornForce += (gravityForce - _physicsData.AirbornForce) * dt;
    if (_physicsData.AirbornForce < gravityForce)
      _physicsData.AirbornForce = gravityForce;
    var playerPos0 = transform.position;
    playerPos0.y += _physicsData.AirbornForce * dt;
    transform.position = playerPos0;

    // Check airborn status
    var raycasthit = new RaycastHit();
    if (Physics.Raycast(new Ray(transform.position + new Vector3(0f, 0.8f, 0f), Vector3.down), out raycasthit, 100f, LayerMask.GetMask("Default")))
    {

      if (raycasthit.distance > 1.8f)
      {
        if (!_physicsData.isAirborn)
        {
          _physicsData.isAirborn = true;
          _physicsData.AirbornTime = Time.time;
        }
      }
      else
      {
        if (_physicsData.isAirborn)
        {
          _physicsData.isAirborn = false;
        }

        //if (_physicsData.AirbornForce < 0f)
        {
          var playerPos = transform.position;
          playerPos.y = raycasthit.point.y + 1f;
          transform.position = playerPos;
        }
      }
    }

    // Jump
    if (gamepad.buttonSouth.wasPressedThisFrame)
    {
      _physicsData.isAirborn = true;
      _physicsData.AirbornForce = 7f;
      _physicsData.AirbornTime = Time.time;
    }

    // Movement
    if (input0.magnitude > 0.4f)
    {
      _physicsData.Velocity += (camera.transform.right * input0.x + cameraForward * input0.y) * dt * 5f;
      var maxV = 1.2f;
      if (_physicsData.Velocity.x > maxV)
        _physicsData.Velocity.x = maxV;
      else if (_physicsData.Velocity.x < -maxV)
        _physicsData.Velocity.x = -maxV;
      if (_physicsData.Velocity.z > maxV)
        _physicsData.Velocity.z = maxV;
      else if (_physicsData.Velocity.z < -maxV)
        _physicsData.Velocity.z = -maxV;

      _physicsData.Velocity += (Vector3.zero - _physicsData.Velocity) * dt * 2f;
    }
    else if (!_physicsData.isAirborn)
      _physicsData.Velocity += (Vector3.zero - _physicsData.Velocity) * dt * 5f;

    var moveDir = (_physicsData.Velocity + _physicsData.ExternalForce) * dt * 5f;
    var moveDirx = new Vector3(moveDir.x, 0f, 0f);
    var moveDirz = new Vector3(0f, 0f, moveDir.z);
    var playerWidth = 0.5f;
    if (Physics.Raycast(new Ray(transform.position + new Vector3(0f, -0.2f, 0f), moveDirx.normalized), out raycasthit, 100f, LayerMask.GetMask("Default")))
    {
      if (raycasthit.distance <= playerWidth)
      {
        //transform.position = new Vector3(raycasthit.point.x, transform.position.y, raycasthit.point.z) - moveDirx.normalized * playerWidth;
        moveDirx = Vector3.zero;
      }
    }
    if (Physics.Raycast(new Ray(transform.position + new Vector3(0f, -0.2f, 0f), moveDirz.normalized), out raycasthit, 100f, LayerMask.GetMask("Default")))
    {
      if (raycasthit.distance <= playerWidth)
      {
        //transform.position = new Vector3(raycasthit.point.x, transform.position.y, raycasthit.point.z) - moveDirz.normalized * playerWidth;
        moveDirz = Vector3.zero;
      }
    }
    transform.position += new Vector3(moveDirx.x, 0f, moveDirz.z);
    transform.Rotate(new Vector3(0f, input1.x * 0.9f, 0f));

    _physicsData.ExternalForce += (Vector3.zero - _physicsData.ExternalForce) * Time.deltaTime * 5f;

    // Camera
    _cameraData.CameraHeight = Mathf.Clamp(_cameraData.CameraHeight + -input1.y * dt * 100f, -85f, 85f);

    var playerForward = transform.forward;
    playerForward.y = 0f;
    playerForward = playerForward.normalized;

    var targetPosition = transform.position + -playerForward * (6.5f + Mathf.Clamp(-_cameraData.CameraHeight, -100f, -20f) * 0.08f) + new Vector3(0f, 2f + _cameraData.CameraHeight * 0.06f, 0f);
    camera.transform.position = targetPosition;

    camera.transform.LookAt(transform.position + playerForward * 5f);

    var ue = camera.transform.localEulerAngles;
    ue.x = _cameraData.CameraHeight;
    camera.transform.localEulerAngles = ue;
    //camera.transform.LookAt(transform.position + playerForward * 1f + new Vector3(0f, _cameraData.CameraHeight, 0f));

    // Aim
    var aimDistance = 35f;
    if (Physics.SphereCast(new Ray(camera.transform.position, camera.transform.forward), 0.05f, out raycasthit, 100f, LayerMask.GetMask("Default", "Enemy")))
      aimDistance = raycasthit.distance;
    //Debug.Log(aimDistance);

    _aimPos =
      transform.position +
      transform.forward * 0.5f +
      new Vector3(0f, 0.4f, 0f);
    _aimDir = ((camera.transform.position + camera.transform.forward * aimDistance) - _aimPos).normalized;

    //Debug.DrawRay(_aimPos, _aimDir * 15f);
  }

  // Take damage
  public override void TakeDamage(DamageData damageData)
  {
    _healthData._Health = Mathf.Clamp(_healthData._Health - damageData.Damage, 0, _healthData._HealthMax);
    _healthData.UpdateUI();
    if (_healthData._Health <= 0f)
    {
      //return;
    }

    if (!isLocalPlayer) return;

    // Recoil
    _physicsData.ExternalForce += (transform.position - damageData.DamageSource.transform.position).normalized * 3f;
  }

  void OnDestroy()
  {
    s_Players.Remove(this);
    s_PlayersMapped.Remove(transform.GetChild(1).gameObject.GetInstanceID());
    s_PlayersNetMapped.Remove(netId);
  }

  //
  CameraData _cameraData;
  class CameraData
  {
    public float CameraHeight = 5f;
    public int CameraRotation;
  }

  // On connect
  [System.Serializable]
  struct SessionData
  {
    public EnemyScript.EnemyType[] SpawnedEnemies;
  }
  [Command]
  void CmdGetSyncSessionData()
  {

    var enemies = EnemyScript.s_Enemies;
    var spawnedEnemies = new EnemyScript.EnemyType[enemies.Count];
    for (var i = 0; i < enemies.Count; i++)
      spawnedEnemies[i] = enemies[i]._EnemyType;

    TargetSyncSessionData(new SessionData()
    {
      SpawnedEnemies = spawnedEnemies,
    });
  }
  [TargetRpc]
  void TargetSyncSessionData(SessionData gotData)
  {

    foreach (var enemyType in gotData.SpawnedEnemies)
    {
      Debug.Log(enemyType);
    }

  }

  //
  public static List<PlayerController> s_Players;
  public static Dictionary<int, PlayerController> s_PlayersMapped;
  public static Dictionary<uint, PlayerController> s_PlayersNetMapped;
  public static void Init()
  {
    s_Players = new();
    s_PlayersMapped = new();
    s_PlayersNetMapped = new();
  }
  public static void UpdateIncr()
  {

  }

  //
  public static PlayerController GetPlayer(Collider c)
  {
    var instanceId = c.gameObject.GetInstanceID();
    if (!s_PlayersMapped.ContainsKey(instanceId)) return null;
    return s_PlayersMapped[instanceId];
  }
  public static PlayerController GetPlayer(uint netId)
  {
    if (!s_PlayersNetMapped.ContainsKey(netId)) return null;
    return s_PlayersNetMapped[netId];
  }
}
