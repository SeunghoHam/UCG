using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Cinemachine;
using DG.Tweening;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace UC
{
    public class UC_CharacterBase : MonoBehaviour
    {
        private CharacterController controller;
        [SerializeField] private CinemachineVirtualCamera _virtualCam;

        [SerializeField] private UC_AnimController _animController;
        [SerializeField] private UC_Canvas _canvas;
        private UC_CameraController cameraController;
        private Rigidbody myRigid;


        private enum State
        {
            SIGHT_1,
            SIGHT_3,
            DASH,
            ATTACK,
            JUMP,
        }

        private State _state = State.SIGHT_1;

        // Compoennt
        [SerializeField] private UC_Mesh meshManager;
        private UC_AttackManager _attackManager;

        private IEnumerator IE_Drift; // 중간 취소를 위해서 Ienumerator 따로선언

        // 키 입력중인지 
        private bool isDashing;
        [SerializeField] private bool isJumping;
        [SerializeField] private bool isLanded;

        // 키 입력 가능여부
        private bool canDash;
        private bool canJump;
        [SerializeField] private bool canAttack;

        // 움직임 관련
        // [점프]
        private bool canSuperJump = false;
        private float curr_Ypos; // 현재 좌표
        private float dest_Ypos; // 목표 좌표  


        // 상태정보
        private bool isTargeting;
        private bool isAttacking; // 연속 공격을 위한 .

        void Awake()
        {
            controller = this.GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = false;
            Screen.SetResolution(1920, 1080,
                FullScreenMode.FullScreenWindow, 0);
            RotateActor = this.transform.GetChild(0).gameObject;

            horizontal = Input.GetAxisRaw("Horizontal");
            vertical = Input.GetAxisRaw("Vertical");

            cameraController = Camera.main.GetComponent<UC_CameraController>();
            meshManager.MeshControl("First");
            cameraController.ChangeSight("First");

            canDash = true;
            canJump = true;
            canAttack = true;

            isJumping = false;
            isLanded = true;
            IE_Drift = CRT_Skill_Slide_DOWN();
            gravityInit();

            _attackManager = this.GetComponent<UC_AttackManager>();
        }

        int count = 0;


        private float _attackingTime = 0f;
        
        void Update()
        {
            if (_state == State.SIGHT_1)
            {
                MouseRot_FirstSight();
                Movement_FirstSight();
            }
            else if (_state == State.SIGHT_3)
            {
                Movement();
                MouseRotator();
            }

            if (isAttacking) // 연속 공격을 위한 타이머 동작해야댐
            {
                _attackingTime += Time.deltaTime;
            }

            //if (Input.GetMouseButtonDown(0))
            if (Input.GetKeyDown(KeyCode.Q)) // 마우스입력하면 에디터가 클릭됨 ..
            {
                if (!canAttack)
                    return;
                canAttack = false;
                _attackManager.NormalAttack();
            }
            
            if(Input.GetKeyDown(KeyCode.E)) // 마우스입력하면 에디터가 클릭됨 ..
            //if (Input.GetMouseButtonDown(1))
            {
                speed = 2f;
                _canvas.mouse_Right_DOWN();
            }
            else if(Input.GetKeyUp(KeyCode.E)) // 마우스입력하면 에디터가 클릭됨 ..
                //else if (Input.GetMouseButtonUp(1))
            {
                speed = 4f;
                _canvas.mouse_Right_UP();
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (!canJump)
                    return;
                canJump = false;
                curr_Ypos = this.gameObject.transform.position.y;
                if (canSuperJump) // 슈퍼점프 
                {
                    // 수치만 좀 더 높게
                    _jumpPower = 3f;
                }
                else // 일반점프
                {
                    _jumpPower = 1.5f;
                    isJumping = true;
                }
            }

            #region 특수스킬

            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                if (canDash)
                {
                    // 카메라 위치 이동
                    //tf_1stCamTf.transform.DOLocalMoveY(-1f, 0.5f).SetEase(Ease.Linear).SetRelative(true);
                    canDash = false;
                    isDashing = true;
                    _canvas.SKILL_DRIFT_DOWN();
                    canSuperJump = true;
                    // 슈퍼점프 가능
                    StartCoroutine(IE_Drift);
                }
            }
            else if (Input.GetKeyUp(KeyCode.LeftShift))
            {
                if (isDashing == true)
                {
                    //tf_1stCamTf.transform.DOLocalMoveY(1f, 0.5f).SetEase(Ease.Linear).SetRelative(true);
                    // 카메라 위치 이동
                    StopCoroutine(IE_Drift);
                    isDashing = false;
                    StartCoroutine(CRT_Skill_Slide_UP());
                }
                // Up 하고 0.5초 이내까지는 슈퍼점프 되도록
            }

            #endregion
        }

        void Input_Move()
        {
        }

        void Input_Attack()
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                meshManager.BodyMesh = true;
            }
            else if (Input.GetKeyDown(KeyCode.Y))
            {
                meshManager.BodyMesh = false;
            }

            if (Input.GetKeyDown(KeyCode.J))
            {
                _animController.Kick();
            }
            else if (Input.GetKeyDown(KeyCode.K))
            {
                _animController.Punch();
            }

            if (Input.GetKeyDown(KeyCode.R)) // [3인칭] close - far 조절
            {
                if (count % 2 == 0)
                {
                    cameraController.ChangeCameraMode("Game");
                }
                else
                {
                    cameraController.ChangeCameraMode("Default");
                }

                count++;
            }

            if (Input.GetKeyDown(KeyCode.U)) // 1인칭 - 3인칭 변경
            {
                if (count % 2 == 0)
                {
                    cameraController.ChangeSight("First");
                    meshManager.MeshControl("First");
                    _state = State.SIGHT_1;
                }
                else
                {
                    cameraController.ChangeSight("Third");
                    meshManager.MeshControl("Third");
                    _state = State.SIGHT_3;
                }

                count++;
            }
        }

        private Vector3 acc; // 중력가속도
        private Vector3 velocity;
        private Vector3 initialVelocity;

        void gravityInit()
        {
            // 중력
            this.initialVelocity = Vector3.down;
            this.acc = Vector3.one;

            // 점프
            this.initJumpVelocity = Vector3.up;
            this.jumpAcc = Vector3.one;
        }

        private void FixedUpdate() // 중력 및 점프
        {
            if (isJumping)
            {
                //Debug.Log("isJumping = true");
                func_Jump();
            }

            if (!isLanded && !isJumping) // 지면 상태가 아니고 / 땅 밟고있지 않음
            {
                //Debug.Log("isLanded= false");
                //this.velocity = this.initialVelocity +(Time.deltaTime * this.acc);
                this.velocity = this.initialVelocity;
                this.transform.position += Time.deltaTime * velocity * 4f;
            }
        }

        private float _jumpPower;
        private Vector3 jumpAcc;
        private Vector3 jumpVelocity;
        private Vector3 initJumpVelocity;

        void func_Jump() // 점프 따로 만들기
        {
            dest_Ypos = curr_Ypos + _jumpPower; // 뒤의 실수 = 점프력
            this.jumpVelocity = this.initJumpVelocity;
            this.transform.position += Time.deltaTime * jumpVelocity * 4f;

            //Debug.Log("현재 위치 : " + this.transform.position.y + ", 목표위치 : " + dest_Ypos);
            if (this.transform.position.y >= dest_Ypos)
            {
                isJumping = false; // 점프 끝
            }
        }

        IEnumerator CRT_Skill_Slide_DOWN() // IE_Drift 에 할당
        {
            Debug.Log("드리프트 다운");
            int count = 0;
            while (isDashing)
            {
                if (count == 2)
                {
                    Debug.Log("대쉬 자동 해제");
                    isDashing = false;
                    canDash = true;
                    StopCoroutine(IE_Drift);
                    StartCoroutine(CRT_Skill_Slide_UP());
                    count = 0;
                }

                yield return new WaitForSeconds(0.5f);
                count++;
                Debug.Log("카운트 증가, 현재 카운트 = " + count);
            }

            Debug.Log("와일문 탈출");
            yield return null;
        }

        IEnumerator CRT_Skill_Slide_UP()
        {
            float _jumpdelay = 0.5f;
            float _cooltime = 1.5f;
            _canvas.SKILL_DRIFT_UP(_cooltime);
            yield return new WaitForSeconds(_jumpdelay);
            canSuperJump = false;
            yield return new WaitForSeconds(_cooltime - _jumpdelay);
            _canvas.COOL_DOWN(0);
            canDash = true;
            yield return null;
        }



        public void Dash()
        {
            _state = State.DASH;
            // 방향 설정
            Vector3 direction = new Vector3(
                Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            
            this.gameObject.transform.DOMove(direction * 4f, 0.3f).SetEase(Ease.Linear).SetRelative(true)
                .OnComplete(()=> _state = State.SIGHT_1);
            
            /*
            this.gameObject.transform.DOLocalMoveZ(4f, 1f)
                .SetEase(Ease.InCirc).SetRelative(true).OnComplete(()=>
                    _state = State.SIGHT_1);*/
        }
        float horizontal; // = Input.GetAxisRaw("Horizontal");
        float vertical; // = Input.GetAxisRaw("Vertical");
        private float speed = 4f;

        bool MoveStart = false;

        void Movement()
        {
            horizontal = Input.GetAxisRaw("Horizontal");
            vertical = Input.GetAxisRaw("Vertical");

            Vector3 direction = new Vector3(horizontal, 0, vertical).normalized; // 이동 각도 설정
            if (direction.magnitude >= 0.1f)
            {
                if (!MoveStart)
                {
                    MoveStart = true;
                    _animController.TriggerMove();
                }

                controller.Move(RotateActor.transform.localRotation * direction * speed * Time.deltaTime);

                _animController.TestDirection(horizontal);
            }
            else
            {
                if (MoveStart)
                {
                    MoveStart = false;
                    _animController.Walk(false);
                    _animController.TestIdle();
                }
            }
        }

        void Movement_FirstSight()
        {
            horizontal = Input.GetAxisRaw("Horizontal");
            vertical = Input.GetAxisRaw("Vertical");


            Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

            if (direction.magnitude >= 0.1f)
            {
                if (!MoveStart)
                {
                    // 1인칭에서는 애니메이션 안보임
                    MoveStart = true;
                }

                controller.Move(RotateActor.transform.localRotation * direction * speed * Time.deltaTime);
            }
            else
            {
                if (MoveStart)
                {
                    MoveStart = false;
                }
            }
        }

        [SerializeField] private CinemachineVirtualCamera cam_1stSight;
        [SerializeField] private Transform tf_1stCamTf;

        void MouseRot_FirstSight()
        {
            mouseX = Input.GetAxisRaw("Mouse X") * _sensitivity;
            mouseY = Input.GetAxisRaw("Mouse Y") * _sensitivity;

            cam_1stSight.transform.position = tf_1stCamTf.position;

            cam_1stSight.transform.eulerAngles +=
                new Vector3(
                    -mouseY,
                    mouseX,
                    0);

            // RotateActor(캐릭터 회전을 위해서) 도 카메라 회전에 맞춰서 회전
            RotateActor.transform.eulerAngles =
                new Vector3(
                    0, cam_1stSight.transform.eulerAngles.y, 0);

            // Ray
        }


        private float mouseX;
        private float mouseY;
        private float _sensitivity = 0.8f;

        private GameObject RotateActor;

        void MouseRotator()
        {
            mouseX = Input.GetAxis("Mouse X") * _sensitivity;
            mouseY = Input.GetAxis("Mouse Y") * _sensitivity;

            RotateActor.transform.eulerAngles +=
                new Vector3(
                    //-Mathf.Clamp(mouseY, -10f, 10f),
                    -0,
                    mouseX,
                    0);
        }

        // 속성 활용하기 ( 속성 값이 변경 될 때마다 일부 코드 실행
        //[SerializeField] private bool landProperty;
        public bool LandProperty
        {
            get { return isLanded; }
            set
            {
                if (isLanded != value)
                {
                    isLanded = value;
                    ChangeLand(isLanded);
                    // 함수 실행
                    //Debug.Log("Property 값 변경됨 : " + value);
                }
                else return;
            }
        }

        public bool JumpProperty
        {
            get { return isJumping; }
        }

        public bool TargetingProperty
        {
            get { return isTargeting; }
            set
            {
                if (isTargeting != value)
                {
                    isTargeting = value;
                    //Debug.Log("타겟팅 하고있는지 여부 변경, 현재 :" + isTargeting);
                    ChangeCrosshair(isTargeting);
                }
                else return;
            }
        }


        void ChangeCrosshair(bool _value)
        {
        }

        void ChangeLand(bool _value)
        {
            if (_value) // canJump 를 true 로 변환
            {
                canJump = true;
            }
            else
            {
            }
        }

        public bool AttackProperty
        {
            get { return canAttack; }
            set
            {
                if (canAttack != value)
                    canAttack = value;
            }
        }

        public bool ContinuousProperty // 연속공격 속성
        {
            get { return isAttacking; }
            set
            {
                if (isAttacking != value)
                    isAttacking = value;
            }
        }
    }
}