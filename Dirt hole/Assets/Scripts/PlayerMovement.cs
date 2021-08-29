using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    struct KeyPress
    {
        public float triggerTime;
        public bool isOn;

        public KeyPress(float _triggerTime, bool _isOn)
        {
            triggerTime = _triggerTime;
            isOn = _isOn;
        }
    }

    public CharacterController controller;
    public float speed = 12f;
    public float gravity = -9.81f;
    public float jumpHeight = 3f;

    float runExtra = 50f;
    public float runSpeed = 0f;

    // public bool jumpRequest = false;
    // public float lastJumpRequest = 0f;

    // public float testX;
    // public float testZ;

    public float activeKeyStrokeTime = 0.05f;

    Vector3 velocity;

    Dictionary<string, KeyPress> keyPresses = new Dictionary<string, KeyPress>();
    List<string> keyNames = new List<string>();

    private void Start()
    {
        keyPresses.Add("Jump", new KeyPress(0, false));
        //keyNames.Add("Jump");


        keyPresses.Add("left shift", new KeyPress(0, false));
        //keyNames.Add("left shift");
    }

    // Update is called once per frame
    void Update()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * (speed + runExtra) * Time.deltaTime);

        //updateInputs();

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        handleJump();
        handleSprint();

        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
        }
    }

    private void handleJump()
    {
        var keyName = "Jump";
        var key = keyPresses[keyName]; 

        if (Input.GetButtonDown(keyName))
        {
            key.triggerTime = Time.time;
            key.isOn = true;
        }
        else
        {
            key.isOn = false;
        }

        if (Time.time - key.triggerTime > activeKeyStrokeTime)
        {
            key.isOn = false;
        }

        keyPresses[keyName] = key;

        if (controller.collisionFlags == CollisionFlags.Below)
        {
            var jumping = keyPresses["Jump"];

            if (jumping.isOn)
            {
                jumping.isOn = false;
                keyPresses["Jump"] = jumping;

                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else
            {
                velocity.y = -2;
            }
        }
    }

    private void handleSprint()
    {
        var sprint = keyPresses["left shift"];

        if (!sprint.isOn && Input.GetKey(KeyCode.LeftShift))
        {
            sprint.isOn = true;
        } else
        {
            sprint.isOn = false;
        }

        if (sprint.isOn)
        {
            runExtra = runSpeed;
        } else
        {
            runExtra = 0;
        }
    }

    private void updateInputs()
    {
        var curTime = Time.time;

        foreach (var keyName in keyNames)
        {
            var key = keyPresses[keyName];

            if (Input.GetButtonDown(keyName))
            {
                key.triggerTime = Time.time;
                key.isOn = true;
            } else
            {
                key.isOn = false;
            }

            if (curTime - key.triggerTime > activeKeyStrokeTime)
            {
                key.isOn = false;
            }

            keyPresses[keyName] = key;
        }
    }
}
