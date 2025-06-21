using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR; // For Valve controller input


public enum Mode
{
    Amd,
    Cataract,
    Glaucoma           //added Glaucoma
};

[System.Serializable]
public class Menus
{
    public GameObject nextTrialPreview;
    public GameObject pauseMenu;
    public GameObject positionMenu;
}

[System.Serializable]
public class PrefabThumbnailPair
{
    public GameObject prefab;
    public GameObject normalPrefab; //added normal prefab
    public Sprite thumbnail;

    public PrefabThumbnailPair(GameObject prefab, GameObject normalPrefab, Sprite thumbnail)
    {
        this.prefab = prefab;
        this.normalPrefab = normalPrefab; //normal prefab cosnt.
        this.thumbnail = thumbnail;
    }
}

public class ScotomaManager : MonoBehaviour
{
    // Add this line at the start of your class.
    private float savedScotomaSize;


    [Header("UI Reference Hand Display")]
    public Image thumbnailDisplay; //To Assign Image component from hand display canvas.
    public Sprite defaultThumbnail;
    public Sprite startThumbnail;


    // Create variables to enable or disable logging functionality for debugging and logging eye tracking
    [Header("Logging  Options")]
    [SerializeField]
    private bool debug_log = true;

    [SerializeField] private bool eye_logging = true;
    [SerializeField] private bool debug_mode = true; // TODO: add easy exit with menu button while in VR

    // Create a new public variable to store the initial scotoma size.
    [Header("Initial Params")]
    [SerializeField]
    public Mode mode = Mode.Amd;

    [SerializeField] private float initialScotomaSize = 0.01199f;
    [SerializeField] private float angleChange = 0.1375f;

    private float amdInitialSize = 0.01199f;
    private float cataractInitialSize = 0.1576167f;
    private float glaucomaInitialsize = 0.35f; //added glaucomaInitialSize, This will not be necessary as we are usign Latin Square Method that assigns size automatically as per the Logic.

    private float amdAngleChange = 0.1375f;
    private float cataractAngleChange = 1f;
    private float glaucomaAngleChange = 1f; //added glaucoma Angle Change //Not necessary for Latin Square Method.

    [Header("Object References")] public GameObject player;
    public GameObject startRoom;

    private ScotomaCalibration scotomaCalibration;
    private ScotomaSimulator scotomaSimulator;
    private CataractParameterSettings cataractParameterSettings;
    private GameObject currentEye;
    public SteamVR_Action_Boolean targetFoundAction; // The TargetFound action
    public Menus menus;

    [Header("Experiment")]
    [SerializeField]
    public int numberOfSearchesPerRoom = 10;

    private List<PrefabThumbnailPair> prefabThumbnailPairs;

    private string[] prefabNames =
    {
        "Apple",
        "Calculator",
        "Duck",
        "FirstAidKit",
        "Flashlight",
        "Glass",
        "Hairdryer",
        "Kettle",
        "Medicine",
        "NoteBook",
        "Phone",
        "RemoteControl",
        "Screwdriver",
        "Soap",
        "Stapler",
        "Eraser",
        "Highlighter",
        "HolePuncher",
        "PencilSharpener",
        "StickyNotes",
        "TapeDispenser",

        //Calculator Material has been assigned to the below following Objects, because all these objects are from the same Office pack and
        //Have the same Master Material.So we do not need to create materials seperately for each object.

        //"Eraser",
        //"Highlighter",
        //"HolePuncher",
        //"PencilSharpener",
        //"StickyNotes",
        //"TapeDispenser",
    };


    public List<GameObject> rooms;

    private GameObject lastSpawnedNormalObject = null; //for tracking normal object spawned in Glaucoma.
    private GameObject lastSpawnedObject = null;
    private GameObject lastPrefab = null;
    private Transform lastSpawnPoint = null;
    private bool isReadyForNextObject = false;
    private int nextPrefabThumbnailPairIndex = -1;
    public int currentRoom = -1;

    private DataLogger dataLogger;
    private int objectCount = 0;

    private DataLoggerEye dataLoggerEye;

    public Camera leftEye;
    public Camera rightEye;

    public GlaucomaCameraCull glaucomaCameraCull; //added referecne to glaucoma camera cull in case Mode = Glaucoma.

    public ScotomaSimulator.Eye singleOrBothEyes;

    ///////////////////////////// BALANCED LATIN SQUARE //////////////////////////////////////////////////////

    private int[,] balancedLatinSquare = new int[,]
    {
        {1, 2, 4, 6, 0, 7, 5, 3}, //0
        {2, 6, 1, 7, 4, 3, 0, 5}, //1
        {6, 7, 2, 3, 1, 5, 4, 0}, //2
        {7, 3, 6, 5, 2, 0, 1, 4}, //3
        {3, 5, 7, 0, 6, 4, 2, 1}, //4
        {5, 0, 3, 4, 7, 1, 6, 2}, //5
        {0, 4, 5, 1, 3, 2, 7, 6}, //6
        {4, 1, 0, 2, 5, 6, 3, 7}, //7
      //{0, 1, 2, 3, 4, 5, 6, 7}
    };

    [SerializeField] public int ParticipantId;
    [SerializeField] public int participantRowNotOverridden;


    public static int participantId { get; private set; }

    private void Awake()
    {
        participantId = ParticipantId;
        UpdateParticipantRow(); //To know which row has been assigned.
    }

    public void UpdateParticipantRow()
    {
        //This gives non overridden rows simply by calculating modulo 8.
        participantRowNotOverridden = participantId % 8;
    }
    
    private Dictionary<int, int> participantRowOverrides;
    [SerializeField] private List<ParticipantRowOverride> participantRowOverridesList = new List<ParticipantRowOverride>();

    [System.Serializable]
    public class ParticipantRowOverride
    {
        public int participantId;
        public int row;

        public ParticipantRowOverride(int participantId, int row)
        {
            this.participantId = participantId;
            this.row = row;
        }
    }

    public float minAngle = 0f;
    public float maxAngle = 120f;
    public float angleDifference = 1f;
    public float[] scotomaSizes;

    public void InitializeScotomaSizes(float minAngle, float maxAngle, float angleDifference)
    {
        scotomaSizes = new float[8];

        for (int i = 0; i < scotomaSizes.Length; i++)
        {
            float angle = minAngle + (angleDifference * i); //calculate each angle

            if (angle > maxAngle)
            {
                angle = maxAngle;
            }

            float radianAngle = angle * Mathf.Deg2Rad; //convert angle to radians
            //scotomaSizes[i] = 2 * scotomaSimulator.scotomaDistance * Mathf.Tan(radianAngle / 2); // Not Working.
            scotomaSizes[i] = 2 * 0.1f * Mathf.Tan(radianAngle / 2); //Using ScotomaDistance = 0.1.

            


            Debug.Log(scotomaSizes[i]);
            
        }


    }

    public void ApplyScotomaSizeForRoom()
    {
        if (mode != Mode.Glaucoma || currentRoom < 0)
        {
            return;
        }

        int participantRow;


        //checks the Dictionary for ID and row, if present will assign the manual value. 
        //Otherwise, It will use the default modulo value to decide the Row for the Participant.
        //This will helpful for manual row assignments for certain participants


        if(!participantRowOverrides.TryGetValue(participantId, out participantRow))
        {
           participantRow = (participantId) % 8; //Values lies between 0-7
           
        }

        int severityLevel = balancedLatinSquare[participantRow, currentRoom]; //currentRoom from 0-7

        float latinScotomaSize = scotomaSizes[severityLevel];
        scotomaSimulator.scotomaSize = latinScotomaSize;

    }

    


    void Start()
    {
       

        //check the preassigned IDs and Rows in the inspector and then add them into the dictionary for manual row assignment.
        participantRowOverrides = new Dictionary<int, int>();
         
        foreach(ParticipantRowOverride overrideEntry in participantRowOverridesList)
        {
            if (!participantRowOverrides.ContainsKey(overrideEntry.participantId))
            {
                participantRowOverrides.Add(overrideEntry.participantId, overrideEntry.row);
            }
        }


        


        InitializeScotomaSizes(minAngle,maxAngle, angleDifference);

       
        StartHandDisplay();//default Loading Display when experiment starts.



        scotomaSimulator = gameObject.GetComponent<ScotomaSimulator>();
        cataractParameterSettings = gameObject.GetComponent<CataractParameterSettings>();
        dataLogger = gameObject.GetComponent<DataLogger>();
        prefabThumbnailPairs = new List<PrefabThumbnailPair>();

        string m = mode switch
        {
            Mode.Amd => "AMD",
            Mode.Cataract => "CAT",
            Mode.Glaucoma => "GLA",    //added glaucoma GLA
            _ => "CAT" //default
        };


        foreach (string name in prefabNames)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{m} Prefabs/{name}.prefab");

            GameObject normalPrefab = null;

            Sprite thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Thumbnails/{name}.png");

            if (mode == Mode.Glaucoma && (singleOrBothEyes == ScotomaSimulator.Eye.Right || singleOrBothEyes == ScotomaSimulator.Eye.Left))
            {   
                //just standard objects in case of single eye
                normalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/GLA normalPrefabs/{name}.prefab");
                Debug.Log("Normal Prefabs Loaded for single Eye");
            }else if (mode == Mode.Glaucoma && singleOrBothEyes == ScotomaSimulator.Eye.Both)
            {
                //In case of both eyes, glaucoma objects are spawned twice. //Shaodows are off in the inspector for GLA2 Objects.
                normalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/GLA2 Prefabs/{name}.prefab");
                Debug.Log("Normal or Special Prefabs Loaded for Glaucoma Dual eye");
            }

            Debug.Log($"Processing {name} for mode {m}"); //to check which prefabs are being added.

            if (prefab == null) Debug.LogWarning($"Prefab not found for {name} in mode {m}.");
            if (mode == Mode.Glaucoma && normalPrefab == null) Debug.LogWarning($"Normal prefab not found for {name} in mode {m}.");
            if (thumbnail == null) Debug.LogWarning($"Thumbnail not found for {name}.");

            // Only add to the list if the Prefab and thumbnail are found. If in GLA mode, also require normalPrefab.

            if (prefab != null && thumbnail != null && (mode != Mode.Glaucoma || normalPrefab != null))
            {
                prefabThumbnailPairs.Add(new PrefabThumbnailPair(prefab, normalPrefab, thumbnail));
            }
        }


        Debug.Log(prefabThumbnailPairs.Count + " Prefabs loaded");

        randomizeRooms(rooms);

        switch (scotomaSimulator.scotomaEyeSelection)
        {
            case ScotomaSimulator.Eye.Left:
                
                currentEye = GameObject.Find("/Player/SteamVRObjects/leftEye");
                scotomaCalibration = currentEye.GetComponent<ScotomaCalibration>();
                Debug.Log("Scotoma Check");

                if (mode == Mode.Cataract)
                {
                    GameObject catPlane = GameObject.Find("/Player/SteamVRObjects/rightEye/rightCatPlane");
                    catPlane.SetActive(true);

                    leftEye.allowHDR = true;

                    // leftEye.GetComponent<CataractRenderPass>().isActive = true;
                }

                break;
            case ScotomaSimulator.Eye.Right:
                
                currentEye = GameObject.Find("/Player/SteamVRObjects/rightEye");
                scotomaCalibration = currentEye.GetComponent<ScotomaCalibration>();


                if (mode == Mode.Cataract)
                {
                    GameObject catPlane = GameObject.Find("/Player/SteamVRObjects/leftEye/leftCatPlane");
                    catPlane.SetActive(true);

                    rightEye.allowHDR = true;

                }

                break;
            case ScotomaSimulator.Eye.Both:

                if(scotomaSimulator.bothEyeCalibration == ScotomaSimulator.DominantEyeBoth.DRight)
                {
                    currentEye = GameObject.Find("/Player/SteamVRObjects/rightEye");
                    scotomaCalibration = currentEye.GetComponent<ScotomaCalibration>();
                }
                else if (scotomaSimulator.bothEyeCalibration == ScotomaSimulator.DominantEyeBoth.DLeft)
                {
                    currentEye = GameObject.Find("/Player/SteamVRObjects/leftEye");
                    scotomaCalibration = currentEye.GetComponent<ScotomaCalibration>();
                }
                

                break;
            default:
                Debug.Log("No eye");
                break;
        }
    }

    public void startLogging()
    {
        dataLoggerEye = new DataLoggerEye();
        dataLoggerEye.StartLogging();
    }

    public void StartScotoma()
    {
        switch (mode)
        {
            case Mode.Amd:
                initialScotomaSize = amdInitialSize;
                angleChange = amdAngleChange;
                break;
            case Mode.Cataract:
                initialScotomaSize = cataractInitialSize;
                angleChange = cataractAngleChange;
                break;
            case Mode.Glaucoma:                                      
                //initialScotomaSize = glaucomaInitialsize; //Don't need these for latin square method.
                //angleChange = glaucomaAngleChange;

                ApplyScotomaSizeForRoom();
                break;

        }

        
        if (eye_logging == true)
        {
            startLogging();
            Debug.Log("Eye Tracking logging started");
        }


        Debug.Log("StartScotoma called");
        scotomaSimulator.scotomaSize = initialScotomaSize;
        if (startRoom == null) Debug.Log("StartRoom is not assigned");
        if (rooms == null || rooms.Count == 0) Debug.Log("Rooms list is not assigned or empty");
        currentRoom = 0; // Start from the first room
        ActivateCurrentRoom();
        currentEye.GetComponent<CataractRenderPass>().isActive = true;
    }

    public void StartTrialButton()
    {
        Debug.Log("StartTrialButton called");
        isReadyForNextObject = true;
        SpawnNextObject();
    }

    private void ActivateCurrentRoom()
    {
        if (currentRoom < rooms.Count)
        {
           
            if (mode == Mode.Glaucoma)
            {
                ApplyScotomaSizeForRoom();
                
            }
            else
            {
                savedScotomaSize = scotomaSimulator.scotomaSize;
                scotomaSimulator.scotomaSize = initialScotomaSize;
            }

            rooms[currentRoom].SetActive(true);
            TeleportPlayer(rooms[currentRoom], "Player_StartPosition");
            MoveToPosition(menus.nextTrialPreview, rooms[currentRoom], "Menus_Position");

            scotomaCalibration.StartCalibration();

            PrepareNextTargetPreview();
            menus.nextTrialPreview.SetActive(true);

            // Start the experiment coroutine
            StartCoroutine(NextTrial());

            // Update the DataLogger
            dataLogger.currentRoomName = rooms[currentRoom].name;
        }
        else
        {
            Debug.Log("All rooms completed!");
            currentRoom = -1;
            TeleportPlayer(startRoom, "Player_StartPosition");
        }
    }

    private void DeactivateCurrentRoom()
    {
        if (currentRoom != -1 && currentRoom < rooms.Count)
        {
            rooms[currentRoom].SetActive(false);
        }
    }


    private IEnumerator NextTrial()
    {
        Debug.Log("NextTrial started");

        Debug.Log("Press WASD to adjust the scotoma");
        Debug.Log("Press enter to finish calibration");
        yield return new WaitUntil(() => scotomaCalibration.IsCalibrationFinished());

        Debug.Log("Calibration finished");


        if (mode == Mode.Glaucoma)
        {
            ApplyScotomaSizeForRoom();
        }

        yield return new WaitUntil(() => isReadyForNextObject);
        
        if (mode != Mode.Glaucoma) //We save the scotomasize only when it is not Glaucoma.
        {
            scotomaSimulator.scotomaSize = savedScotomaSize;
        }


        SpawnNextObject();
        isReadyForNextObject = false;

        for (int i = 1; i <= numberOfSearchesPerRoom; i++) // Ensure the loop goes until numberOfSearchesPerRoom
        {
            objectCount++;
            Debug.Log("Waiting for target to be found...");
            yield return new WaitUntil(() => targetFoundAction.GetStateDown(SteamVR_Input_Sources.Any));
            dataLogger.logEndOfObject();

            ResetHandDisplay(); //Reset the hand display once the displayed object has been found.
            DestroyLastSpawnedObject();

            if (i < numberOfSearchesPerRoom)
            {
                Debug.Log("Target found, preparing next object...");
                PrepareNextTargetPreview();
                menus.nextTrialPreview.SetActive(true);
                yield return new WaitUntil(() => isReadyForNextObject);

                

                if (mode != Mode.Glaucoma)
                {
                    DecreaseScotomaSize(); //Decrease Scotoma only when not in GLA Mode
                }


                cataractParameterSettings.ApplyDeltaToParams();
                SpawnNextObject();
                isReadyForNextObject = false;
            }
            else // If this is the 15th object, don't prepare next target preview, but show positioning menu
            {
                Debug.Log("All objects found, showing position menu...");
                // Show the position menu before going to the next room or finishing the experiment
                MoveToPosition(menus.positionMenu, rooms[currentRoom], "Menus_Position");
                menus.positionMenu.SetActive(true);
                // Show the positioning plane before going to the next room or finishing the experiment
                GameObject positioningPlane =
                    rooms[currentRoom].transform.Find("Player_StartPosition/Plane").gameObject;
                if (positioningPlane != null)
                {
                    positioningPlane.SetActive(true);
                }
                else
                {
                    Debug.LogError("Positioning Plane not found in room: " + rooms[currentRoom].name);
                }

                yield return new WaitUntil(() => !menus.positionMenu.activeSelf);
                // Hide the positioning plane before moving to next room
                if (positioningPlane != null)
                {
                    positioningPlane.SetActive(false);
                }
            }
        }


        if (currentRoom == 3)
        {
            Debug.Log("Room 2 completed, showing break menu...");
            yield return ShowBreakMenuThenProceed();
        }
        else if (currentRoom < rooms.Count - 1)
        {
            Debug.Log("All ten objects found in the current room, moving to next room...");
            NextTrialButton();
        }
        else
        {
            Debug.Log("All rooms completed!");
            currentRoom = -1;
            TeleportPlayer(startRoom, "Player_StartPosition");
        }
    }


    private IEnumerator ShowBreakMenuThenProceed()
    {
        MoveToPosition(menus.pauseMenu, rooms[currentRoom], "Menus_Position");
        menus.pauseMenu.SetActive(true);

        dataLoggerEye.PauseLogging();

        yield return new WaitUntil(() => !menus.pauseMenu.activeSelf);
        dataLoggerEye.ResumeLogging();


        MoveToPosition(menus.positionMenu, rooms[currentRoom], "Menus_Position");
        menus.positionMenu.SetActive(true);
        // Show the positioning plane during the break
        GameObject positioningPlane = rooms[currentRoom].transform.Find("Player_StartPosition/Plane").gameObject;
        if (positioningPlane != null)
        {
            positioningPlane.SetActive(true);
        }

        yield return new WaitUntil(() => !menus.positionMenu.activeSelf);
        // Hide the positioning plane before moving to next room
        if (positioningPlane != null)
        {
            positioningPlane.SetActive(false);
        }

        DeactivateCurrentRoom();
        currentRoom++;
        ActivateCurrentRoom();
    }

    private void PrepareNextTargetPreview()
    {
        Image thumbnail = menus.nextTrialPreview.transform.Find("Thumbnail").GetComponent<Image>();
        Text targetName = menus.nextTrialPreview.transform.Find("TargetName").GetComponent<Text>();

        if (prefabThumbnailPairs.Count > 0)
        {
            if (prefabThumbnailPairs.Count == 1)
            {
                Debug.LogError("Only one prefab found. Need more than one to prevent repetition.");
                return;
            }

            do
            {
                nextPrefabThumbnailPairIndex = UnityEngine.Random.Range(0, prefabThumbnailPairs.Count);
            } while (prefabThumbnailPairs[nextPrefabThumbnailPairIndex].prefab == lastPrefab);

            lastPrefab = prefabThumbnailPairs[nextPrefabThumbnailPairIndex].prefab;
            thumbnail.sprite = prefabThumbnailPairs[nextPrefabThumbnailPairIndex].thumbnail;
            targetName.text = prefabThumbnailPairs[nextPrefabThumbnailPairIndex].prefab.name;
        }
        else
        {
            Debug.LogError("Prefab-thumbnail pairs list is empty");
        }
    }


    private void SpawnNextObject()
    {
        if (!isReadyForNextObject)
        {
            Debug.LogError("Not ready for next object yet.");
            return;
        }

        Transform objectSpawnPoints = rooms[currentRoom].transform.Find("ObjectSpawnPoints");
        if (objectSpawnPoints != null)
        {
            Vector3 spawnVector;
            if (objectSpawnPoints.childCount == 1)
            {
                Debug.LogError("Only one spawn point found. Need more than one to prevent repetition.");
                return;
            }

            Transform spawnPoint;
            do
            {
                int randomSpawnPointIndex = UnityEngine.Random.Range(0, objectSpawnPoints.childCount);
                spawnPoint = objectSpawnPoints.GetChild(randomSpawnPointIndex);
                bool spawnPointFound = false;
                spawnVector = spawnPoint.position;
                if (spawnPoint.childCount == 1)
                {
                    int tryCount = 0;
                    do
                    {
                        tryCount++;
                        Transform parent = spawnPoint;
                        Transform child = spawnPoint.GetChild(0);


                        //Choose a random position between parent and child :)
                        Vector3 randomPosition = new Vector3(UnityEngine.Random.Range(parent.position.x, child.position.x), parent.position.y, UnityEngine.Random.Range(parent.position.z, child.position.z));



                        //Spawn object at random positons.
                        if (CheckSpawnPoint(randomPosition))
                        {
                            spawnPointFound = true;
                            spawnVector = randomPosition;
                        }
                        else
                        {
                            Debug.Log("Cannot spawn at this Position");
                        }
                    } while (spawnPointFound == false && tryCount < 500);
                }

            } while (spawnPoint == lastSpawnPoint);

            lastSpawnPoint = spawnPoint;

            if (lastSpawnedObject != null)
            {
                Destroy(lastSpawnedObject);
            }

            if (lastSpawnedNormalObject != null)
            {
                Destroy(lastSpawnedNormalObject);
            }


            GameObject newObject = Instantiate(prefabThumbnailPairs[nextPrefabThumbnailPairIndex].prefab, spawnVector, Quaternion.identity);
            //quaternion.identity to have no rotation or default rotation.

            UpdateHandDisplay(prefabThumbnailPairs[nextPrefabThumbnailPairIndex].thumbnail); //To Update the Hand Display after every Spawn.

            Debug.Log("Spawned object: " + newObject.name);
            lastSpawnedObject = newObject; // Track the Glaucoma/cat/amd object

            GameObject normalObject = null; //Initializing normal object to null.

            if (mode == Mode.Glaucoma)
            {
                normalObject = Instantiate(prefabThumbnailPairs[nextPrefabThumbnailPairIndex].normalPrefab, spawnVector, Quaternion.identity);

                // To make sure both normal and glaucoma objects have same transformations.Since both glaucoma and normal objects exist for duplicate spawning
                normalObject.transform.position = newObject.transform.position;
                normalObject.transform.rotation = newObject.transform.rotation;
                normalObject.transform.localScale = newObject.transform.localScale;

                Debug.Log("Spawned Dual (normal or Fade eye dependent) object: " + normalObject.name);
                lastSpawnedNormalObject = normalObject; // Track and save the normal object only in GLA mode for destroying later along with glaucoma object.

            }

            UpdateDataLoggerFields(newObject, spawnPoint); // Update logger with special object details

            dataLogger.logStartOfObject();
            isReadyForNextObject = false; // Reset the flag here


        }
        else
        {
            Debug.LogError("ObjectSpawnPoints not found in room: " + rooms[currentRoom].name);
        }

    }


   

    public void UpdateHandDisplay(Sprite thumbnail) //This will Display the Current Target object on the Hand Dispaly.
    {
        if(thumbnailDisplay != null)
        {
            thumbnailDisplay.sprite = thumbnail;
        }else
        {
            Debug.LogError("Thumbnail display not set up correctly in the Inspector");
        }
    }

    public void ResetHandDisplay() //This will reset back to the default Thumbanil assigned in the inspector.
    {
        if (thumbnailDisplay != null)
        {
            thumbnailDisplay.sprite = defaultThumbnail;
        }
        else
        {
            Debug.LogError("Default thumbnail sprite not set up correctly in the Inspector");
        }
    }

    public void StartHandDisplay() //This will display the start thumbnail.
    {
        if (thumbnailDisplay != null)
        {
            thumbnailDisplay.sprite = startThumbnail;
        }
        else
        {
            Debug.LogError("start thumbnail sprite not set up correctly in the Inspector");
        }
    }

    private bool CheckSpawnPoint(Vector3 position)
    {
        bool canspawn = true;

        Collider[] colliders = Physics.OverlapBox(new Vector3(position.x, (float)(position.y + (0.7369973 * 0.3199501) / 2 + 0.00001), position.z), new Vector3((float)(1.948 * 0.135 / 2), (float)(0.7369973 * 0.3199501 / 2), (float)(1.061213 * 0.2683688 / 2)), Quaternion.identity);

        if (colliders.Length != 0)
        {

            foreach (var c in colliders)
            {
                Debug.Log("colliding with " + c.name);
            }

            canspawn = false;
        }

        return canspawn;
    }

    public void DestroyLastSpawnedObject()
    {
        if (lastSpawnedObject != null)
        {
            Destroy(lastSpawnedObject);
            lastSpawnedObject = null;
        }
        else
        {
            Debug.LogError("No object to destroy");
        }


        if (lastSpawnedObject != null)
        {
            Destroy(lastSpawnedObject);
            lastSpawnedObject = null;
        }

        if (mode == Mode.Glaucoma && lastSpawnedNormalObject != null)
        { // Only check for the normal object in GLA mode
            Destroy(lastSpawnedNormalObject);
            lastSpawnedNormalObject = null;
        }

        if (lastSpawnedObject == null && (mode != Mode.Glaucoma || lastSpawnedNormalObject == null))
        {
            Debug.Log("Both special and normal objects (if any) have been destroyed");
        }
        else
        {
            Debug.LogError("There was an issue destroying objects. One or both objects may not have been destroyed properly.");
        }



    }

    private void UpdateDataLoggerFields(GameObject newObject, Transform spawnPoint)
    {
        

        dataLogger.currentObject = newObject;
        dataLogger.currentSpawnPointName = spawnPoint.name;
        dataLogger.objectCount = objectCount;

    }

    private void TeleportPlayer(GameObject room, string positionName)
    {
        Transform playerStartPosition = room.transform.Find(positionName);
        if (playerStartPosition != null)
        {
            player.transform.position = playerStartPosition.position;
            player.transform.rotation = playerStartPosition.rotation;
        }
        else
        {
            Debug.LogError("Player_StartPosition not found in room: " + room.name);
        }
    }

    private void MoveToPosition(GameObject objectToMove, GameObject room, string positionName)
    {
        Transform targetPosition = room.transform.Find(positionName);
        if (targetPosition != null)
        {
            objectToMove.transform.position = targetPosition.position;
            objectToMove.transform.rotation = targetPosition.rotation;
        }
        else
        {
            Debug.LogError(positionName + " not found in room: " + room.name);
        }
    }

    public void DecreaseScotomaSize() //Not necessary for Latin Square Method.
    {
        // Convert the decrease amount from degrees to radians.

        float currentAngel = scotomaSimulator.totalScotomaCoverageDegrees / 2; //120
        float newAngle = currentAngel - angleChange / 2; //119

        float oldSize = Mathf.Tan(currentAngel * Mathf.Deg2Rad) * scotomaSimulator.scotomaDistance;
        float newSize = Mathf.Tan(newAngle * Mathf.Deg2Rad) * scotomaSimulator.scotomaDistance;

        float sizeDif = oldSize - newSize;

        // float decreaseRadians = angleChange * Mathf.Deg2Rad;

        // Calculate the new scotoma size.
        // float newSize = Mathf.Tan(decreaseRadians) * scotomaSimulator.scotomaDistance;

        float decrease = initialScotomaSize / 120;

        // Update the scotoma size.
        scotomaSimulator.scotomaSize -= 6 * sizeDif;
        //scotomaSimulator.scotomaSize -= 2 * sizeDif;

        Debug.Log($"Decrease amount in degrees: {angleChange}");
        // Debug.Log($"Decrease amount in radians: {decreaseRadians}");
        Debug.Log($"Decrease amount in size: {oldSize}");
        Debug.Log($"Decrease amount in size: {newSize}");
        Debug.Log($"Decrease amount in size: {scotomaSimulator.totalScotomaCoverageDegrees}");

    }

    static void randomizeRooms<GameObject>(List<GameObject> rooms)
    {
        System.Random rng = new System.Random();
        int n = rooms.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            GameObject temp = rooms[k];
            rooms[k] = rooms[n];
            rooms[n] = temp;
        }

        Debug.Log("Ranomized Rooms");
    }

    // Buttons
    public void OKButtonPressed()
    {
        Debug.Log("OK Button Pressed");
        menus.nextTrialPreview.SetActive(false);
        isReadyForNextObject = true;
    }

    public void ContinuePositioning()
    {
        menus.positionMenu.SetActive(false);
    }

    public void ContinueExperiment()
    {
        menus.pauseMenu.SetActive(false);
    }

    public void NextTrialButton()
    {
        Debug.Log("NextTrialButton called");
        DeactivateCurrentRoom();
        currentRoom++;
        ActivateCurrentRoom();
    }

    public void Quit()
    {
        Application.Quit();
    }
}

