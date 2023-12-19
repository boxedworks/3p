using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
  // Start is called before the first frame update
  void Start()
  {
    _physicsData = new();
    _cameraData = new();
  }

  PhysicsData _physicsData;
  class PhysicsData
  {
    public Vector3 Velocity;

    public bool isAirborn = true;
    public float AirbornTime;
    public float AirbornForce;

  }

  // Update is called once per frame
  void Update()
  {

    // Gather controller input
    var gamepad = Gamepad.current;

    var input0 = Vector2.zero;
    var input1 = Vector2.zero;
    input0 = gamepad.leftStick.value;
    input1 = gamepad.rightStick.value;

    var camera = Camera.main;
    var cameraForward = camera.transform.forward;
    cameraForward.y = 0f;
    cameraForward = cameraForward.normalized;

    var gravityForce = -17f;
    _physicsData.AirbornForce += (gravityForce - _physicsData.AirbornForce) * Time.deltaTime;
    if (_physicsData.AirbornForce < gravityForce)
      _physicsData.AirbornForce = gravityForce;
    var playerPos0 = transform.position;
    playerPos0.y += _physicsData.AirbornForce * Time.deltaTime;
    transform.position = playerPos0;

    // Airborn
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
      _physicsData.Velocity += (camera.transform.right * input0.x + cameraForward * input0.y) * Time.deltaTime * 5f;
      var maxV = 2f;
      if (_physicsData.Velocity.x > maxV)
        _physicsData.Velocity.x = maxV;
      else if (_physicsData.Velocity.x < -maxV)
        _physicsData.Velocity.x = -maxV;
      if (_physicsData.Velocity.z > maxV)
        _physicsData.Velocity.z = maxV;
      else if (_physicsData.Velocity.z < -maxV)
        _physicsData.Velocity.z = -maxV;

      _physicsData.Velocity += (Vector3.zero - _physicsData.Velocity) * Time.deltaTime * 2f;
    }
    else
      _physicsData.Velocity += (Vector3.zero - _physicsData.Velocity) * Time.deltaTime * 5f;

    var moveDir = _physicsData.Velocity * Time.deltaTime * 5f;
    var moveDirx = new Vector3(moveDir.x, 0f, 0f);
    var moveDirz = new Vector3(0f, 0f, moveDir.z);
    var playerWidth = 0.5f;
    if (Physics.Raycast(new Ray(transform.position + new Vector3(0f, -0.2f, 0f), moveDirx.normalized), out raycasthit, 100f, LayerMask.GetMask("Default")))
    {
      if (raycasthit.distance <= playerWidth)
      {
        transform.position = new Vector3(raycasthit.point.x, transform.position.y, raycasthit.point.z) - moveDirx.normalized * playerWidth;
        moveDirx = Vector3.zero;
      }
    }
    if (Physics.Raycast(new Ray(transform.position + new Vector3(0f, -0.2f, 0f), moveDirz.normalized), out raycasthit, 100f, LayerMask.GetMask("Default")))
    {
      if (raycasthit.distance <= playerWidth)
      {
        transform.position = new Vector3(raycasthit.point.x, transform.position.y, raycasthit.point.z) - moveDirz.normalized * playerWidth;
        moveDirz = Vector3.zero;
      }
    }

    transform.position += new Vector3(moveDirx.x, 0f, moveDirz.z);
    transform.Rotate(new Vector3(0f, input1.x * 0.9f, 0f));

    // Camera
    _cameraData.CameraHeight = Mathf.Clamp(_cameraData.CameraHeight + -input1.y * Time.deltaTime * 100f, -85f, 85f);

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
  }

  CameraData _cameraData;
  class CameraData
  {
    public float CameraHeight = 5f;
    public int CameraRotation;
  }
}
