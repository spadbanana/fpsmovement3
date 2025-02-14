using Godot;
using System;
using System.Data;

public partial class Player : CharacterBody3D
{	
	private CollisionShape3D lowerCollisionShape;
	private CollisionShape3D upperCollisionShape;

	private Marker3D neckPivotDesiredPosition;
	private Node3D neckPivot;
	private Node3D headPivot;
	private Node3D eyesPivot;

	private AnimationPlayer neckAnimationPlayer;
	private ShapeCast3D headShapeCast;

	private Node3D ledgeDetector;
	private RayCast3D ledgeRayCast;
	private Marker3D ledgeIndicator;
	private ShapeCast3D standingShapeCast;
	private ShapeCast3D crouchingShapeCast;

	private ShapeCast3D wallShapeCast;
	private Marker3D wallIndicator;

	private float rotationPitch = 0.0f; //MOUSEMOVEMENT HANDLER
	private float rotationYaw = 0.0f;
	public float mouseSensitivity = 0.001f;
	public float pitchClamp = Mathf.Pi/2;
	public float yawClamp = 0.0f;

	private readonly StateMachine movementMachine = new(); //STATEMACHINES
	public State runningState { get; private set; }
	public State crouchingState { get; private set; }
	public State slidingState { get; private set; }
	public State walkingState { get; private set; }
	public State sprintingState { get; private set; }

	private readonly StateMachine jumpingMachine = new();
	public State fallingState { get; private set; }
	public State landedState{ get; private set; }
	public State jumpingState { get; private set; }
	
	private readonly StateMachine vaultingMachine = new();
	public State notVaultingState { get; private set; }
	public State vaultingStandingToStandingState { get; private set; }
	public State vaultingStandingToCrouchingState { get; private set; }
	public State vaultingCrouchingToCrouchingState { get; private set; }
	public State airVaulting { get; private set; }

	private enum VaultDetectionMode 
	{
		cannotVault,
		canVault,
		canOnlyVaultCrouching
	}
	private VaultDetectionMode vaultDetectionMode = VaultDetectionMode.cannotVault;

	public bool jumpNextPhysicsProcess = false; //STORED PROCESS INPUTS
	public bool crouchNextPhysicsProcess = false;
	public bool airVaultNextPhysicsProcess  = false;

	private Vector3 lastVelocity = Vector3.Zero;
	private Vector3 lastRotatedLerp = Vector3.Zero;
	private Vector3 lastlerpInputDir = Vector3.Zero;

	private Vector2 inputDir = Vector2.Zero;
	private Vector3 inputDirNorm = Vector3.Zero;
	private Vector3 lerpInputDir = Vector3.Zero;
	public float lerpInputSpeed = 10.0f; //INPUT LERP
	private Vector3 rotatedLerpInput = Vector3.Zero;
	private Vector3 direction = Vector3.Zero;

	private float standingShapeCastShapeHeight = 1.8f;
	private float standingShapeCastOriginOffset = 0.0f;
	private float crouchingShapeCastShapeHeight = 1.2f;
	private float crouchingShapeCastOriginOffset = 0.0f;
	
	private float lowerCollisionShapeOriginOffset = 0.5f;
	private float upperCollisionShapeOriginOffset = 0.5f;
	private float wallShapeCastOriginOffset = 0.6f;
	private Vector3 lowerCollisionShapeDesiredPosition = Vector3.Zero;
	private Vector3 upperCollisionShapeDesiredPosition = Vector3.Zero;

	private float movementMachineSpeed = 5.0f;
	public float crouchingSpeed = 2.0f;
	public float walkingSpeed = 3.0f;
	public float runningSpeed = 5.0f;
	public float sprintingSpeed = 8.0f;

	public float lerpCurrentHeightSpeed = 10.0f;
	public float lerpMovementMachineSpeedSpeed = 20.0f;
	[Export]
	public float lerpMovementMachineSpeedSlideDecay = 1.5f;
	public float laddMovementMachineSpeedSprintSpeed = 0.5f;

	private Vector2 groundVelocity = Vector2.Zero;

	private float fallgravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
	private float fallRatio = 2.0f;
	public float playerJumpHeight = 1.15f;
	[Export]
	public float lerpJumpDecay = 10f;
	public float distanceFellDuringFalling = 0f;

	public float movementBoostAmount = 0.05f;
	public float lerpMovementBoost = 1f;
	private Vector3 movementBoost = Vector3.Zero;

	public float pertubateRatio = 0.15f;
	private Vector3 pertubateMovement = Vector3.Zero;
	private float headBobbingCurrentSpeed = 22f;
	private Vector2 headBobbingVector = Vector2.Zero;
	private float headBobbingIndex = 0.0f;
	public float lerpEyePivot = 15f;
	private Vector2 eyeRotation = Vector2.Zero;
	private float sprintingRunningDifference = 0f;

	private Vector3 wallHit = Vector3.Zero;
	private int collisionCount = 0;
	private Vector3 ledgeHit = Vector3.Zero;
	private Basis rotatedInputBasis = Basis.Identity;
	private float ledgeDetectorTolerance = 0.05f;

	private float stepHeight = 0f;
	private float ledgeDistance = 0f;
	private Vector3 ledgeDirection = Vector3.Zero;
	private float vaultMin = 0.25f;
	private float vaultMax = 1.15f;
	private Vector3 vaultStoredRotatedLerp = Vector3.Zero;
	private Vector3 vaultStoredInputDirection = Vector3.Zero;
	public float vaultJumpHeight = 0.15f;
	private float vaultStepUpTolerance = 0.1f;
	private bool vaultingFreezeMovementMachine = false;

	private float airVaultFallingMax = 0f;

	private bool neckPivotTransformUpdated = true;
	private Transform3D previousNeckPivotDesiredPosition = Transform3D.Identity;
	private Transform3D currentNeckPivotDesiredPosition = Transform3D.Identity;

	
	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;

		lowerCollisionShape = GetNode<CollisionShape3D>("LowerCollisionShape");
		upperCollisionShape = GetNode<CollisionShape3D>("UpperCollisionShape");

		neckPivotDesiredPosition = GetNode<Marker3D>("NeckPivotDesiredPosition");
		neckPivot = GetNode<Node3D>("NeckPivot");
		headPivot = GetNode<Node3D>("NeckPivot/HeadPivot");
		eyesPivot = GetNode<Node3D>("NeckPivot/HeadPivot/EyesPivot");
		neckAnimationPlayer = GetNode<AnimationPlayer>("NeckPivot/NeckAnimationPlayer");

		headShapeCast = GetNode<ShapeCast3D>("HeadShapeCast");

		ledgeDetector = GetNode<Node3D>("LedgeDetector");
		ledgeRayCast = GetNode<RayCast3D>("LedgeDetector/LedgeRayCast");
		ledgeIndicator = GetNode<Marker3D>("LedgeDetector/LedgeRayCast/LedgeIndicator");
		standingShapeCast = GetNode<ShapeCast3D>("LedgeDetector/StandingShapeCast");
		crouchingShapeCast = GetNode<ShapeCast3D>("LedgeDetector/CrouchingShapeCast");

		wallShapeCast = GetNode<ShapeCast3D>("WallShapeCast");
		wallIndicator = GetNode<Marker3D>("WallShapeCast/WallIndicator");

		runningState = movementMachine.CreateState(UpdateRunning, OnEnterRunning, null, "running");
		crouchingState = movementMachine.CreateState(UpdateCrouching, OnEnterCrouching, null, "crouching");
		slidingState = movementMachine.CreateState(UpdateSliding, OnEnterSliding, OnExitSliding, "sliding");
		walkingState = movementMachine.CreateState(UpdateWalking, OnEnterWalking, null, "walking");
		sprintingState = movementMachine.CreateState(UpdateSprinting, OnEnterSprinting, null, "sprinting");

		fallingState = jumpingMachine.CreateState(UpdateFalling, OnEnterFalling, null, "falling");
		landedState = jumpingMachine.CreateState(UpdateLanded, OnEnterLanded, null, "landed");
		jumpingState = jumpingMachine.CreateState(UpdateJumping, OnEnterJumping, null, "jumping");

		notVaultingState = vaultingMachine.CreateState(null, null, null, "notVaulting");
		vaultingStandingToStandingState = vaultingMachine.CreateState(UpdateVaultingStandingToStanding, OnEnterVaultingStandingToStanding, OnExitVaultingStandingToStanding, "vaultingStandingToStanding");
		vaultingStandingToCrouchingState = vaultingMachine.CreateState(UpdateVaultingStandingToCrouching, OnEnterStandingToCrouching, OnExitStandingToCrouching, "vaultingStandingToCrouching");
		vaultingCrouchingToCrouchingState = vaultingMachine.CreateState(UpdateVaultingCrouchingToCrouching, OnEnterCrouchingToCrouching, OnExitCrouchingToCrouching, "vaultingCrouchingToCrouching");
		airVaulting = vaultingMachine.CreateState(UpdateAirVaulting, OnEnterAirVaulting, OnExitAirVaulting, "airVaulting");
		
		movementMachine.SetState(runningState);
		jumpingMachine.SetState(fallingState);
		vaultingMachine.SetState(notVaultingState);

		neckPivot.TopLevel = true;
		previousNeckPivotDesiredPosition = neckPivotDesiredPosition.GlobalTransform;
		currentNeckPivotDesiredPosition = neckPivotDesiredPosition.GlobalTransform;
	}

	public override void _Input(InputEvent @event)
    {
		if (@event is InputEventMouseMotion inputEventMouseMotion)
		{
			rotationYaw -= inputEventMouseMotion.Relative.X * mouseSensitivity;

			if(yawClamp != 0)
			{
				rotationYaw = Mathf.Clamp(rotationYaw, -yawClamp * (Mathf.Pi/180), yawClamp * (Mathf.Pi/180));
			}

			var playerTransform = Transform;
			playerTransform.Basis = Basis.Identity;
			playerTransform.Basis = new Basis(Vector3.Up, rotationYaw) * playerTransform.Basis;
			Transform = playerTransform;

			rotationPitch -= inputEventMouseMotion.Relative.Y * mouseSensitivity;

			rotationPitch = Mathf.Clamp(rotationPitch , -pitchClamp, pitchClamp);
			
			var headPivotTransform = headPivot.Transform;
			headPivotTransform.Basis = Basis.Identity;
			headPivotTransform.Basis = new Basis(Vector3.Right, rotationPitch) * headPivotTransform.Basis;
			headPivot.Transform = headPivotTransform;
		}
    }

	public override void _Process(double delta)
	{
		StoredInputForNextPhysicsProcess();
		GetInputDirNorm();
		UpdateNeckRealPosition();
	}

	public override void _PhysicsProcess(double delta)
	{
		GetLastFrameVariables();
		SetMovementMode();
		SetVaultingMode();
		SetJumpingMode();
		LerpInputDirection(delta);
		RunStateUpdates(delta);
		ApplyGravity(delta);
		UpdatePlayerDirection();

		UpdateNeckPosition();
		UpdateHeadBobbing(delta);

		SetVelocity();

		GD.Print(movementMachine.CurrentState());
		GD.Print(jumpingMachine.CurrentState());
		GD.Print(vaultingMachine.CurrentState());
		//GD.Print(lowerCollisionShape.Position);
		//GD.Print(upperCollisionShape.Position);
		//GD.Print(lowerCollisionShapeDesiredPosition);
		//GD.Print(upperCollisionShapeDesiredPosition);
		GD.Print(Velocity.Y);

		MoveAndSlide();
		LedgeDetection();
		ClearStoredInputForNextPhysicsProcess();
	}

	public void StoredInputForNextPhysicsProcess()
	{
		if (Input.IsActionJustPressed("jump") && jumpingMachine.CurrentState() == "landed" && vaultingMachine.CurrentState() == "notVaulting")
		{
			jumpNextPhysicsProcess = true;
		}
		if (Input.IsActionPressed("jump") && jumpingMachine.CurrentState() != "landed" && vaultingMachine.CurrentState() == "notVaulting")
		{
			airVaultNextPhysicsProcess = true;
		}
		if (Input.IsActionPressed("crouch"))
		{
			crouchNextPhysicsProcess = true;
		}
	}
	public void GetInputDirNorm()
	{
		inputDir = Input.GetVector("left" , "right" , "forward" , "back");
		inputDirNorm = new Vector3(inputDir.X , 0 , inputDir.Y).Normalized();
	}
	public void UpdateNeckRealPosition()
	{
		if (neckPivotTransformUpdated)
		{
			previousNeckPivotDesiredPosition = currentNeckPivotDesiredPosition;
			currentNeckPivotDesiredPosition = neckPivotDesiredPosition.GlobalTransform;
			neckPivotTransformUpdated = false;
		}

		var frameInterpFraction = Mathf.Clamp(Engine.GetPhysicsInterpolationFraction(), 0, 1);
		neckPivot.GlobalTransform = previousNeckPivotDesiredPosition.InterpolateWith(currentNeckPivotDesiredPosition, (float)frameInterpFraction);
	}

	public void GetLastFrameVariables()
	{
		lastVelocity = Velocity;
		groundVelocity = new(Velocity.X , Velocity.Z);
	}
	public void SetMovementMode()
	{
		if (vaultingFreezeMovementMachine)
		{
			return;
		}
		
		if (crouchNextPhysicsProcess || headShapeCast.IsColliding())
		{
			if(groundVelocity.LengthSquared() > 18 && jumpingMachine.CurrentState() == "landed")
			{
				movementMachine.SetStateIfNotCurrentState(slidingState);
			}
			else
			{
				movementMachine.SetStateIfNotCurrentState(crouchingState);
			}
		}
		else if (!headShapeCast.IsColliding())
		{
			if (Input.IsActionPressed("walk"))
			{
				movementMachine.SetStateIfNotCurrentState(walkingState);
			}
			else if (groundVelocity.LengthSquared() > 24.9 && inputDirNorm == Vector3.Forward)
			{
				movementMachine.SetStateIfNotCurrentState(sprintingState);
			}
			else
			{
				movementMachine.SetStateIfNotCurrentState(runningState);
			}
		}
	}
	public void SetVaultingMode()
	{
		if (vaultingMachine.CurrentState() != "notVaulting" || movementMachine.CurrentState() == "sliding")
		{
			return;			
		}
		if (inputDir == new Vector2(0,1) || inputDir == new Vector2(-1,1) || inputDir == new Vector2(1,1) || inputDir == new Vector2(-1,0) || inputDir == new Vector2(1,0))
		{
			return;
		}
		if (!wallShapeCast.IsColliding())
		{
			return;
		}
		if (stepHeight < vaultMin || vaultMax < stepHeight)
		{
			return;
		}
		if (groundVelocity.LengthSquared() <= 0.01 && 0.37 < ledgeDistance)
		{
			return;
		}
		if (groundVelocity.LengthSquared() <= 9 && 0.5 < ledgeDistance)
		{
			return;
		}	
		if (groundVelocity.LengthSquared() <= 25 && 0.75 < ledgeDistance)
		{
			return;
		}	
		if (groundVelocity.LengthSquared() <= 64 && 1.25 < ledgeDistance)
		{
			return;
		}
		if (jumpNextPhysicsProcess && jumpingMachine.CurrentState() == "landed")
		{	
			if (vaultDetectionMode == VaultDetectionMode.canOnlyVaultCrouching && movementMachine.CurrentState() == "crouching")
			{
				vaultingMachine.SetStateIfNotCurrentState(vaultingCrouchingToCrouchingState);
			}
			else if(vaultDetectionMode == VaultDetectionMode.canOnlyVaultCrouching)
			{
				if(rotationPitch > 0 || stepHeight < 0.6f)
				{
					return;
				}
				vaultingMachine.SetStateIfNotCurrentState(vaultingStandingToCrouchingState);
			}
			else if (vaultDetectionMode == VaultDetectionMode.canVault && movementMachine.CurrentState() == "crouching")
			{
				vaultingMachine.SetStateIfNotCurrentState(vaultingCrouchingToCrouchingState);
			}
			else if (vaultDetectionMode == VaultDetectionMode.canVault)
			{
				vaultingMachine.SetStateIfNotCurrentState(vaultingStandingToStandingState);
			}
		}
		if (Velocity.Y < airVaultFallingMax)
		{
			return;
		}
		if (movementMachine.CurrentState() == "crouching" || movementMachine.CurrentState() == "sliding")
		{
			return;
		}
		if(airVaultNextPhysicsProcess && jumpingMachine.CurrentState() == "jumping")
		{
			if (vaultDetectionMode == VaultDetectionMode.canVault)
			{
				vaultingMachine.SetStateIfNotCurrentState(airVaulting);
			}
		}
	}
	public void SetJumpingMode()
	{
		if (!IsOnFloor())
		{
			if (Velocity.Y <= 0)
			{
				jumpingMachine.SetStateIfNotCurrentState(fallingState);
			}
		}
		else
		{
			if(jumpNextPhysicsProcess && jumpingMachine.CurrentState() == "landed" && vaultingMachine.CurrentState() == "notVaulting")
			{
				jumpingMachine.SetStateIfNotCurrentState(jumpingState);
			}
			else if (Velocity.Y <= 0)
			{
				jumpingMachine.SetStateIfNotCurrentState(landedState);
			}
		}
	}
	public void LerpInputDirection(double delta)
	{
		if (direction.Length() - groundVelocity.Length()/movementMachineSpeed > 0.25 )
		{
			lerpInputDir = lerpInputDir.Lerp(inputDirNorm * 0.1f, (float)delta * 2 * lerpInputSpeed);
		}
		else
		{
			lerpInputDir = lerpInputDir.Lerp(inputDirNorm, (float)delta * lerpInputSpeed);
		}	
	}
	public void RunStateUpdates(double delta)
	{	
		movementMachine.Tick(delta);
		jumpingMachine.Tick(delta);
		vaultingMachine.Tick(delta);
	}
	public void ApplyGravity(double delta)
	{
		lastVelocity.Y -= fallgravity * (float)delta * fallRatio;
	}
	public void UpdatePlayerDirection()
	{
		direction = rotatedLerpInput + movementBoost + pertubateMovement;
	}
	public void UpdateNeckPosition()
	{
		neckPivot.Position = upperCollisionShape.Position + new Vector3(0, upperCollisionShapeOriginOffset - 0.1f, 0);
		neckPivotDesiredPosition.Position = upperCollisionShape.Position + new Vector3(0, upperCollisionShapeOriginOffset - 0.1f, 0);

		neckPivotTransformUpdated = true;
	}
	public void UpdateHeadBobbing(double delta)
	{
		if(jumpingMachine.CurrentState() == "landed" && movementMachine.CurrentState() == "sprinting")
		{	
			sprintingRunningDifference = (groundVelocity.Length() - runningSpeed)/(sprintingSpeed - runningSpeed);
			if (sprintingRunningDifference > 0)
			{
				headBobbingCurrentSpeed = 22f * sprintingRunningDifference;
				headBobbingIndex += headBobbingCurrentSpeed * (float)delta;

				headBobbingVector.Y = -Mathf.Sin(headBobbingIndex/2) + 0.5f;
				headBobbingVector.X = Mathf.Sin(headBobbingIndex);

				headBobbingVector *= sprintingRunningDifference;
			}
		}

		if (direction.LengthSquared() >= 0.01f) //VIEWBOB
		{
			var eyesTransform = eyesPivot.Transform;
			eyesTransform.Basis = Basis.Identity;
			eyesTransform.Basis = new Basis(Vector3.Up , headBobbingVector.Y * (Mathf.Pi/180)) * eyesTransform.Basis;
			eyesTransform.Basis = new Basis(Vector3.Right, headBobbingVector.X * (Mathf.Pi/180)) * eyesTransform.Basis;
			eyesPivot.Transform = eyesTransform;
		}
	}
	public void SetVelocity()
	{
		lastVelocity.X = direction.X * movementMachineSpeed;
		lastVelocity.Z = direction.Z * movementMachineSpeed;

		Velocity = lastVelocity;
	}
	public void LedgeDetection()
	{
		collisionCount = wallShapeCast.GetCollisionCount();
		ledgeHit = ledgeRayCast.GetCollisionPoint();

		if (inputDirNorm != Vector3.Zero)
		{
			rotatedInputBasis = Basis.LookingAt(inputDirNorm, Vector3.Up);
			wallShapeCast.Enabled = true;
		}
		else
		{
			rotatedInputBasis = Basis.LookingAt(Vector3.Forward, Vector3.Up);
		}
		wallShapeCast.Basis = rotatedInputBasis;

		if (wallShapeCast.IsColliding())
		{
			wallHit = wallShapeCast.GetCollisionPoint(collisionCount - 1);

			if (!standingShapeCast.IsColliding())
			{
				vaultDetectionMode = VaultDetectionMode.canVault;
			}
			else if (!crouchingShapeCast.IsColliding())
			{
				vaultDetectionMode = VaultDetectionMode.canOnlyVaultCrouching;
			}
			else
			{
				vaultDetectionMode = VaultDetectionMode.cannotVault;
			}

			wallIndicator.GlobalPosition = wallHit;

			ledgeDetectorTolerance = 0.05f;

			var wallIndicatorDirection = GlobalPosition - wallIndicator.GlobalPosition;
			wallIndicatorDirection = new Vector3(wallIndicatorDirection.X, 0f, wallIndicatorDirection.Z).Normalized();

			standingShapeCastOriginOffset = standingShapeCastShapeHeight/2f;
			crouchingShapeCastOriginOffset = crouchingShapeCastShapeHeight/2f;

			ledgeDetector.GlobalPosition = wallHit + (standingShapeCastShapeHeight * Vector3.Up) - wallIndicatorDirection * ledgeDetectorTolerance;
			ledgeIndicator.GlobalPosition = ledgeHit;

			standingShapeCast.GlobalPosition = ledgeHit + (ledgeDetectorTolerance + standingShapeCastOriginOffset) * Vector3.Up;
			crouchingShapeCast.GlobalPosition = ledgeHit  + (ledgeDetectorTolerance + crouchingShapeCastOriginOffset) * Vector3.Up;

			if (ledgeRayCast.IsColliding())
			{
				stepHeight = ledgeIndicator.GlobalPosition.Y - GlobalPosition.Y;
				ledgeDirection = ledgeIndicator.GlobalPosition - GlobalPosition;
				ledgeDirection = new Vector3 (ledgeDirection.X, 0, ledgeDirection.Z);
				ledgeDistance = ledgeDirection.Length();
				ledgeDirection = ledgeDirection.Normalized();
			}
		}
		else
		{
			stepHeight = 0f;
			ledgeDistance = 1f;
			vaultDetectionMode = VaultDetectionMode.cannotVault;
		}
	}

	public void ClearStoredInputForNextPhysicsProcess()
	{
		jumpNextPhysicsProcess = false;
		airVaultNextPhysicsProcess = false;
		crouchNextPhysicsProcess = false;
	}

	public void UpdateCollisionShapePosition(double delta)
	{		
		var lowerCollisionShapePosition = lowerCollisionShape.Position;
		lowerCollisionShapePosition = lowerCollisionShapePosition.Lerp(lowerCollisionShapeDesiredPosition, (float)delta * lerpCurrentHeightSpeed);
		lowerCollisionShapePosition= lowerCollisionShapePosition.Clamp(new Vector3 (0, 0.5f, 0),new Vector3 (0, 1.3f, 0));
		lowerCollisionShape.Position = lowerCollisionShapePosition;

		wallShapeCast.Position = lowerCollisionShape.Position + new Vector3(0, wallShapeCastOriginOffset - lowerCollisionShapeOriginOffset, 0);

		var upperCollisionShapePosition = upperCollisionShape.Position;
		upperCollisionShapePosition = upperCollisionShapePosition.Lerp(upperCollisionShapeDesiredPosition, (float)delta * lerpCurrentHeightSpeed);
		upperCollisionShape.Position = upperCollisionShapePosition;
	}

	public void SetDesiredPositionStanding()
	{
		lowerCollisionShapeDesiredPosition = new Vector3 (0, lowerCollisionShapeOriginOffset, 0);
		upperCollisionShapeDesiredPosition = new Vector3 (0, 0.8f + upperCollisionShapeOriginOffset, 0);
	}

	public void SetDesiredPositionCrouching()
	{
		lowerCollisionShapeDesiredPosition = new Vector3 (0, lowerCollisionShapeOriginOffset, 0);
		upperCollisionShapeDesiredPosition = new Vector3 (0, 0.2f + upperCollisionShapeOriginOffset, 0);
	}
	public void UpdateCrouching(double delta)
	{
		UpdateCollisionShapePosition(delta);

		if (jumpingMachine.CurrentState() == "landed")
		{
			movementMachineSpeed += (crouchingSpeed - movementMachineSpeed) * (float)delta * lerpMovementMachineSpeedSpeed;
		}
	}
	public void OnEnterCrouching()
	{	
		SetDesiredPositionCrouching();
	}

	public void UpdateSliding(double delta)
	{
		UpdateCollisionShapePosition(delta);
		
		if (jumpingMachine.CurrentState() == "landed")
		{
			movementMachineSpeed += (crouchingSpeed - movementMachineSpeed) * (float)delta * lerpMovementMachineSpeedSlideDecay;

			rotatedLerpInput = lastRotatedLerp;

			pertubateMovement = Transform.Basis * lerpInputDir * pertubateRatio;
		}
	}
	public void OnEnterSliding()
	{
		lowerCollisionShapeDesiredPosition = new Vector3 (0, lowerCollisionShapeOriginOffset, 0);
		upperCollisionShapeDesiredPosition = new Vector3 (0, upperCollisionShapeOriginOffset, 0);

		lastlerpInputDir = lerpInputDir;

		lastRotatedLerp = rotatedLerpInput;
	}
	public void OnExitSliding()
	{
		lerpInputDir = lastlerpInputDir;

		if (rotatedLerpInput.Normalized().Dot(lastRotatedLerp.Normalized()) <= 0.95 && inputDirNorm != Vector3.Zero)
		{
			lerpInputDir *= 0.5f;
		}

		if (rotatedLerpInput.Normalized().Dot(lastRotatedLerp.Normalized()) <= 0.45 && inputDirNorm != Vector3.Zero)
		{
			lerpInputDir = Vector3.Zero;
		}

		pertubateMovement = Vector3.Zero;		
	}

	public void UpdateWalking(double delta)
	{
		UpdateCollisionShapePosition(delta);

		movementMachineSpeed += (walkingSpeed - movementMachineSpeed) * (float)delta * lerpMovementMachineSpeedSpeed;
	}
	public void OnEnterWalking()
	{
		SetDesiredPositionStanding();
	}

	public void UpdateSprinting(double delta)
	{
		UpdateCollisionShapePosition(delta);

		movementMachineSpeed += (sprintingSpeed - runningSpeed) * (float)delta * laddMovementMachineSpeedSprintSpeed;
		movementMachineSpeed = Mathf.Clamp(movementMachineSpeed, runningSpeed, sprintingSpeed);
	}
	public void OnEnterSprinting()
	{
		SetDesiredPositionStanding();
	}

	public void UpdateRunning(double delta)
	{
		UpdateCollisionShapePosition(delta);

		movementMachineSpeed += (runningSpeed - movementMachineSpeed) * (float)delta * lerpMovementMachineSpeedSpeed;
	}
	public void OnEnterRunning()
	{	
		SetDesiredPositionStanding();
	}

	public void UpdateFalling(double delta)
	{	
		rotatedLerpInput = lastRotatedLerp.Lerp(Vector3.Zero , (float)delta * lerpJumpDecay);

		movementBoost = movementBoost.Lerp(Vector3.Zero , (float)delta * lerpMovementBoost);

		if (groundVelocity.LengthSquared() <= 18)
		{
			pertubateMovement = Transform.Basis * lerpInputDir * pertubateRatio;
		}

		distanceFellDuringFalling += Mathf.Abs(lastVelocity.Y * (float)delta);
	}
	public void OnEnterFalling()
	{	
		lastRotatedLerp = rotatedLerpInput;

		distanceFellDuringFalling = 0f;
	}
	public void UpdateLanded(double delta)
	{
		if(movementMachine.CurrentState() != "sliding")
		{
			rotatedLerpInput = Transform.Basis * lerpInputDir;

			pertubateMovement = Vector3.Zero;
		}
	}
	public void OnEnterLanded()
	{	
		if(distanceFellDuringFalling > playerJumpHeight * 0.25f)
		{
			//neckAnimationPlayer.Play("land");
		}

		rotatedLerpInput = Transform.Basis * lerpInputDir;

		movementBoost = Vector3.Zero;

		pertubateMovement = Vector3.Zero;

		if (rotatedLerpInput.Normalized().Dot(lastRotatedLerp.Normalized()) <= 0.95 && inputDirNorm != Vector3.Zero)
		{
			lerpInputDir *= 0.5f;
		}

		if (rotatedLerpInput.Normalized().Dot(lastRotatedLerp.Normalized()) <= 0.45 && inputDirNorm != Vector3.Zero)
		{
			lerpInputDir = Vector3.Zero;
		}
	}
	public void UpdateJumping(double delta)
	{
		rotatedLerpInput = lastRotatedLerp;

		movementBoost = movementBoost.Lerp(Vector3.Zero , (float)delta * lerpMovementBoost);

		if (groundVelocity.LengthSquared() <= 18 )
		{
			pertubateMovement = Transform.Basis * lerpInputDir * pertubateRatio;
		}
	}
	public void OnEnterJumping()
	{
		lastVelocity.Y += Mathf.Sqrt(2 * fallgravity * fallRatio * playerJumpHeight);
		//neckAnimationPlayer.Play("jump");

		rotatedLerpInput = Transform.Basis * lerpInputDir;

		lastRotatedLerp = rotatedLerpInput;

		if (groundVelocity.LengthSquared() <= 18 && inputDirNorm == Vector3.Forward && movementMachine.CurrentState() != "crouching" && movementMachine.CurrentState() != "sliding")
		{
			movementBoost = Transform.Basis * lerpInputDir * movementBoostAmount;
		}
	}
	public void UpdateVaultingStandingToStanding(double delta)
	{
		rotatedLerpInput = 0.8f * vaultStoredRotatedLerp;

		if(IsOnFloor() && Velocity.Y <= 0)
		{
			vaultingMachine.SetStateIfNotCurrentState(notVaultingState);
		}
	}

	public void OnEnterVaultingStandingToStanding()
	{
		vaultingFreezeMovementMachine = true;

		if(inputDir != Vector2.Zero)
		{
			vaultStoredRotatedLerp = (float)Mathf.Max(rotatedLerpInput.Length(), 0.5f) * rotatedLerpInput.Normalized();
			vaultStoredInputDirection = (float)Mathf.Max(lerpInputDir.Length(), 0.8f) * lerpInputDir.Normalized();
		}
		else
		{
			vaultStoredRotatedLerp = Transform.Basis * (0.5f * Vector3.Forward);
			vaultStoredInputDirection = 0.8f * Vector3.Forward;
		}
		
		vaultJumpHeight = stepHeight/2;
		lastVelocity.Y += Mathf.Sqrt(2 * fallgravity * fallRatio * vaultJumpHeight);
		
		var stepUp = stepHeight - vaultJumpHeight;

		lowerCollisionShapeDesiredPosition = new Vector3 (0, lowerCollisionShapeOriginOffset + stepUp, 0);
		lowerCollisionShape.Position = lowerCollisionShapeDesiredPosition;
	}

	public void OnExitVaultingStandingToStanding()
	{
		vaultingFreezeMovementMachine = false;

		rotatedLerpInput = 0.8f * vaultStoredRotatedLerp;

		lerpInputDir = vaultStoredInputDirection;

		SetDesiredPositionStanding();
	}

	public void UpdateVaultingCrouchingToCrouching(double delta)
	{
		rotatedLerpInput = 0.8f * vaultStoredRotatedLerp;

		if(IsOnFloor() && Velocity.Y <= 0)
		{
			vaultingMachine.SetStateIfNotCurrentState(notVaultingState);
		}
	}

	public void OnEnterCrouchingToCrouching()
	{
		vaultingFreezeMovementMachine = true;

		if(inputDir != Vector2.Zero)
		{
			vaultStoredRotatedLerp = (float)Mathf.Max(rotatedLerpInput.Length(), 0.5f) * rotatedLerpInput.Normalized();
			vaultStoredInputDirection = (float)Mathf.Max(lerpInputDir.Length(), 0.8f) * lerpInputDir.Normalized();
		}
		else
		{
			vaultStoredRotatedLerp = Transform.Basis * (0.5f * Vector3.Forward);
			vaultStoredInputDirection = 0.8f * Vector3.Forward;
		}
		
		vaultJumpHeight = stepHeight - 0.2f;
		lastVelocity.Y += Mathf.Sqrt(2 * fallgravity * fallRatio * vaultJumpHeight);
		
		var stepUp = stepHeight - vaultJumpHeight;

		lowerCollisionShapeDesiredPosition = new Vector3 (0, lowerCollisionShapeOriginOffset + stepUp, 0);
		lowerCollisionShape.Position = lowerCollisionShapeDesiredPosition;
	}

	public void OnExitCrouchingToCrouching()
	{
		vaultingFreezeMovementMachine = false;

		rotatedLerpInput = 0.8f * vaultStoredRotatedLerp;

		lerpInputDir = vaultStoredInputDirection;

		SetDesiredPositionCrouching();
	}

	public void UpdateVaultingStandingToCrouching(double delta) 
	{
		rotatedLerpInput = 0.8f * vaultStoredRotatedLerp;

		if(IsOnFloor() && Velocity.Y <= 0)
		{
			vaultingMachine.SetStateIfNotCurrentState(notVaultingState);
		}
	}
	public void OnEnterStandingToCrouching() 
	{
		vaultingFreezeMovementMachine = true;

		if(inputDir != Vector2.Zero)
		{
			vaultStoredRotatedLerp = (float)Mathf.Max(rotatedLerpInput.Length(), 0.5f) * rotatedLerpInput.Normalized();
			vaultStoredInputDirection = (float)Mathf.Max(lerpInputDir.Length(), 0.8f) * lerpInputDir.Normalized();
		}
		else
		{
			vaultStoredRotatedLerp = Transform.Basis * (0.5f * Vector3.Forward);
			vaultStoredInputDirection = 0.8f * Vector3.Forward;
		}
		
		vaultJumpHeight = stepHeight/2;
		lastVelocity.Y += Mathf.Sqrt(2 * fallgravity * fallRatio * vaultJumpHeight);
		
		var stepUp = stepHeight - vaultJumpHeight;

		lowerCollisionShapeDesiredPosition = new Vector3 (0, lowerCollisionShapeOriginOffset + stepUp, 0);
		lowerCollisionShape.Position = lowerCollisionShapeDesiredPosition;

		upperCollisionShapeDesiredPosition = lowerCollisionShapeDesiredPosition + new Vector3(0, 0.2f, 0);
	}
	public void OnExitStandingToCrouching() 	
	{
		vaultingFreezeMovementMachine = false;

		rotatedLerpInput = 0.8f * vaultStoredRotatedLerp;

		lerpInputDir = vaultStoredInputDirection;

		SetDesiredPositionCrouching();
	}

	public void UpdateAirVaulting(double delta )
	{
		rotatedLerpInput = 0.8f * vaultStoredRotatedLerp;

		if(Velocity.Y <= 0)
		{
			jumpingMachine.SetStateIfNotCurrentState(fallingState);

			SetDesiredPositionStanding();
		}
		if(IsOnFloor() && Velocity.Y <= 0)
		{
			vaultingMachine.SetStateIfNotCurrentState(notVaultingState);
		}
	}
	public void OnEnterAirVaulting()
	{
		vaultingFreezeMovementMachine = true;

		if(inputDir != Vector2.Zero)
		{
			vaultStoredRotatedLerp = (float)Mathf.Max(rotatedLerpInput.Length(), 0.5f) * rotatedLerpInput.Normalized();
			vaultStoredInputDirection = (float)Mathf.Max(lerpInputDir.Length(), 0.8f) * lerpInputDir.Normalized();
		}
		else
		{
			vaultStoredRotatedLerp = Transform.Basis * (0.5f * Vector3.Forward);
			vaultStoredInputDirection = 0.8f * Vector3.Forward;
		}

		lastVelocity.Y += 0.2f * lastVelocity.Y + 2f;
		
		var stepUp = stepHeight + vaultStepUpTolerance;

		lowerCollisionShapeDesiredPosition = new Vector3 (0, lowerCollisionShapeOriginOffset + stepUp, 0);
		lowerCollisionShape.Position = lowerCollisionShapeDesiredPosition;
	}
	public void OnExitAirVaulting()
	{
		vaultingFreezeMovementMachine = false;

		rotatedLerpInput = 0.8f * vaultStoredRotatedLerp;

		lerpInputDir = vaultStoredInputDirection;

		if (rotatedLerpInput.Normalized().Dot(lastRotatedLerp.Normalized()) <= 0.95 && inputDirNorm != Vector3.Zero)
		{
			lerpInputDir = 0.5f * vaultStoredInputDirection;
		}

		if (rotatedLerpInput.Normalized().Dot(lastRotatedLerp.Normalized()) <= 0.45 && inputDirNorm != Vector3.Zero)
		{
			lerpInputDir = Vector3.Zero;
		}

		SetDesiredPositionStanding();
	}
}

