using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileScript : MonoBehaviour
{

  Collider _collider;
  Rigidbody _rigidBody;
  UnityEngine.VFX.VisualEffect _vfx;

  float _spawnTime;
  ProjectileSpawnData _projectileSpawnData;

  // Start is called before the first frame update
  public void Init(ProjectileSpawnData projectileSpawnData)
  {
    _projectileSpawnData = projectileSpawnData;

    _spawnTime = Time.time;
    gameObject.layer = 8;

    // Physics
    _collider = GetComponent<Collider>();
    _collider.isTrigger = true;
    Physics.IgnoreCollision(_collider, projectileSpawnData.Source.transform.GetChild(1).GetComponent<Collider>(), true);

    _rigidBody = gameObject.AddComponent<Rigidbody>();
    _rigidBody.interpolation = RigidbodyInterpolation.Interpolate;
    _rigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

    _rigidBody.position = projectileSpawnData.SpawnPosition;
    _rigidBody.AddForce(projectileSpawnData.Direction * projectileSpawnData.Speed);


    // FX
    GameObject.Destroy(GetComponent<Renderer>());

    _vfx = GameObject.Instantiate(GameObject.Find("Fireball")).GetComponent<UnityEngine.VFX.VisualEffect>();
    _vfx.transform.parent = transform;
    _vfx.transform.localPosition = Vector3.zero;
  }

  // Update is called once per frame
  void Update()
  {
    if (Time.time - _spawnTime > 20f)
    {
      OnDestroy();
    }
  }

  void FixedUpdate()
  {

  }

  //
  bool _triggered;
  void OnTriggerEnter(Collider c)
  {
    if (_triggered) return;
    Debug.Log(c.name);

    switch (c.gameObject.layer)
    {

      // Map
      case 0:
      default:
        break;

      // Projectile
      case 8:
        return;

      // Player
      case 6:

        var player = PlayerController.GetPlayer(c);
        if (player == null) break;
        player.TakeDamage(new GameEntity.DamageData()
        {
          Damage = _projectileSpawnData.Damage,
          DamageSource = _projectileSpawnData.Source
        });

        break;

      // Enemy
      case 7:

        var enemy = EnemyScript.GetEnemy(c);
        if (enemy == null) break;
        enemy.TakeDamage(new GameEntity.DamageData()
        {
          Damage = _projectileSpawnData.Damage,
          DamageSource = _projectileSpawnData.Source
        });

        break;

    }

    _triggered = true;
    OnDestroy();
  }

  //
  void OnDestroy()
  {
    _triggered = true;
    _collider.enabled = false;
    _rigidBody.isKinematic = true;

    _vfx.Stop();

    GameObject.Destroy(gameObject, 1f);
  }

  //
  public struct ProjectileSpawnData
  {
    public Vector3 SpawnPosition, Direction;
    public float Size, Speed;
    public GameEntity Source;
    public ProjectileType ProjectileType;

    public float Damage;
  }
  public enum ProjectileType
  {
    NONE,

    FIREBALL,
  }
  public static ProjectileScript SpawnProjectile(ProjectileSpawnData projectileSpawnData)
  {

    var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

    var size = projectileSpawnData.Size;
    sphere.transform.localScale = new Vector3(size, size, size);

    var projectileScript = sphere.AddComponent<ProjectileScript>();
    projectileScript.Init(projectileSpawnData);

    return projectileScript;
  }
}
