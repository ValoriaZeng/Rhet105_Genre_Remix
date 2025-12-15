using UnityEngine;
using UnityEngine.UI; // Required for basic UI elements like Image and AspectRatioFitter
using TMPro; // Required for TextMeshPro elements (Text, InputField, Button)
using System.Collections.Generic; // Required for Dictionary

// --- Helper classes to organize story data ---

// A struct to hold the points for each choice
public struct PlayerPoints
{
    public int logic;
    public int ethics;
    public int compassion;
}

// A class to define what a choice button does
public class ChoiceData
{
    public string text;
    public string nextSceneKey;
    public PlayerPoints points;
}

// A class to define all the data for a single scene
public class SceneData
{
    public string characterName;
    public string dialogueText;
    public string portraitKey;
    public string nextSceneKey; // Key for the next scene (if no choices)
    public ChoiceData[] choices; // Array of choices (if any)
}

// --- The Main Dialogue Manager Script ---

public class DialogueManager : MonoBehaviour
{
    [Header("Player State")]
    private PlayerPoints playerPoints;
    private string playerName;
    private string currentSceneKey;

    [Header("Game History")]
    private PlayerPoints previousScore;
    private bool hasPreviousScore = false;

    [Header("UI Panels")]
    public GameObject titlePanel; // <--- NEW: Title Panel
    public GameObject nameEntryPanel;
    public GameObject dialoguePanel;
    public Transform choiceContainer; // The parent object for choice buttons (with a Vertical Layout Group)

    [Header("Name Entry UI")]
    public TMP_InputField playerNameInput;

    [Header("Dialogue UI")]
    public TextMeshProUGUI characterNameText;
    public TextMeshProUGUI dialogueText;
    public GameObject characterPortraitContainer; // The container/parent object for the portrait

    [Header("Prefabs & Assets")]
    public GameObject choiceButtonPrefab; // A prefab for your choice buttons
    public Sprite judgePortrait;
    public Sprite prosecutorPortrait;
    public Sprite defensePortrait;
    public Sprite defendantPortrait; // Use a generic "defendant" portrait or a custom one
    public Sprite narratorPortrait;

    [Header("Internal Data")]
    private Dictionary<string, SceneData> storyScenes;
    private Dictionary<string, Sprite> portraitSprites;


    // Internal references for dynamic sizing and ratio
    private Image portraitVisual; 
    private AspectRatioFitter aspectRatioFitter;
    
    // For dynamic scaling of the Narrator portrait
    private RectTransform portraitContainerRectTransform;
    private Vector3 originalScale;
    private const float NARRATOR_SCALE_FACTOR = 1f; // Scale factor for Narrator only

    void Awake()
    {
        // 1. Get the Image component from the child object named "PortraitVisual" 
        if (characterPortraitContainer != null)
        {
            portraitVisual = characterPortraitContainer.GetComponentInChildren<Image>();
            
            // Get the RectTransform of the container and store its scale
            portraitContainerRectTransform = characterPortraitContainer.GetComponent<RectTransform>();
            if (portraitContainerRectTransform != null)
            {
                // Store the default scale for later restoration
                originalScale = portraitContainerRectTransform.localScale; 
            }

            // 2. Get the AspectRatioFitter from the same object as the Image
            if (portraitVisual != null)
            {
                aspectRatioFitter = portraitVisual.GetComponent<AspectRatioFitter>();
            }
        }

        if (portraitVisual == null && characterPortraitContainer != null)
        {
            Debug.LogError("PortraitVisual (Image component) not found as a child of the Character Portrait Container.");
        }
        
        if (aspectRatioFitter == null && portraitVisual != null)
        {
            Debug.LogError("AspectRatioFitter component not found on PortraitVisual. Please add it!");
        }
    }

    void Start()
    {
        // Initialize dictionaries (REQUIRED)
        storyScenes = new Dictionary<string, SceneData>();
        portraitSprites = new Dictionary<string, Sprite>();

        // Load data
        InitializePortraits();
        InitializeStory();

        // Set up initial game state
        playerPoints = new PlayerPoints { logic = 0, ethics = 0, compassion = 0 };
        
        // Show title screen first
        currentSceneKey = "title"; // <--- NEW: Set starting scene to title
        titlePanel.SetActive(true); // <--- NEW: Activate title panel
        nameEntryPanel.SetActive(false);
        dialoguePanel.SetActive(false);
        choiceContainer.gameObject.SetActive(false);

        // --- UPDATED: Set the placeholder text for the InputField ---
        if (playerNameInput != null && playerNameInput.placeholder != null)
        {
            // The placeholder is usually a TextMeshProUGUI component attached to the placeholder object
            (playerNameInput.placeholder as TextMeshProUGUI).text = "enter name...";
        }
        // --- END UPDATED ---
    }
    
    // <--- NEW FUNCTION: Start the game from the title screen --->
    public void StartGame()
    {
        // Hide title panel
        titlePanel.SetActive(false);

        // Show name entry screen
        nameEntryPanel.SetActive(true);
    }
    // <--- END NEW FUNCTION --->

    // Called by the "Submit" button on the name entry panel
    public void SubmitName()
    {
        playerName = playerNameInput.text;
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Dr. "; // Default name if none is entered
        }
        else
        {
            playerName = "Dr. " + playerName; // Add the "Dr." prefix
        }

        // NEW: Check for previous score based on the entered name
        LoadPreviousScore(playerName);

        // Hide name panel, show dialogue panel
        nameEntryPanel.SetActive(false);
        dialoguePanel.SetActive(true);

        if (hasPreviousScore)
        {
            // If previous score exists, show the intro scene first
            currentSceneKey = "score_display_intro";
        }
        else
        {
            // Otherwise, start the main story
            currentSceneKey = "start";
        }
        RenderScene();
    }

    // NEW: Load score using PlayerPrefs (simulating persistence)
    void LoadPreviousScore(string name)
    {
        string key = "Score_" + name;
        if (PlayerPrefs.HasKey(key))
        {
            string data = PlayerPrefs.GetString(key);
            string[] parts = data.Split('|');

            // Data format: L:X|E:Y|C:Z
            if (parts.Length == 3)
            {
                // Extracting values safely
                int.TryParse(parts[0].Substring(2), out previousScore.logic);
                int.TryParse(parts[1].Substring(2), out previousScore.ethics);
                int.TryParse(parts[2].Substring(2), out previousScore.compassion);
                hasPreviousScore = true;
                Debug.Log($"Loaded previous score for {name}: L:{previousScore.logic}, E:{previousScore.ethics}, C:{previousScore.compassion}");
            }
            else
            {
                Debug.LogWarning("PlayerPrefs data corrupted for key: " + key);
                hasPreviousScore = false;
            }
        }
        else
        {
            hasPreviousScore = false;
        }
    }

    // NEW: Save score using PlayerPrefs
    void SaveCurrentScore()
    {
        string key = "Score_" + playerName;
        // Store as a simple pipe-separated string
        string data = $"L:{playerPoints.logic}|E:{playerPoints.ethics}|C:{playerPoints.compassion}";
        PlayerPrefs.SetString(key, data);
        PlayerPrefs.Save(); // Commit changes to disk
        Debug.Log($"Saved current score for {playerName}: {data}");
    }

    // Load portrait sprites into the dictionary
    void InitializePortraits()
    {
        portraitSprites["judge"] = judgePortrait;
        portraitSprites["prosecutor"] = prosecutorPortrait;
        portraitSprites["defense"] = defensePortrait;
        portraitSprites["defendant"] = defendantPortrait;
        portraitSprites["none"] = narratorPortrait; // Key for the Narrator portrait
    }

    // This is where the entire story tree is defined
    void InitializeStory()
    {
        // We use {PLAYER_NAME} as a placeholder that will be replaced.
        
        // <--- NEW SCENE: Title Screen Data --->
        storyScenes["title"] = new SceneData
        {
            characterName = "Designing Future",
            portraitKey = "none",
            dialogueText = "\n\n(Click anywhere to start)",
            nextSceneKey = null // This scene is handled by StartGame()
        };
        // <--- END NEW SCENE --->
        
        // NEW: Scene to display previous score
        storyScenes["score_display_intro"] = new SceneData
        {
            characterName = "System Log",
            portraitKey = "none",
            // Use {PREV_...} placeholders to show loaded score
            dialogueText = "Welcome back, {PLAYER_NAME}. Your previous trial resulted in: Logic: {PREV_LOGIC}, Ethics: {PREV_ETHICS}, Compassion: {PREV_COMPASSION}. This new trial begins with a clean slate.",
            nextSceneKey = "start"
        };
        
        storyScenes["start"] = new SceneData
        {
            characterName = "Narrator",
            portraitKey = "none",
            dialogueText = "The courtroom is silent. You sit at the defense table next to your colleague, the Lead Defense Attorney. {PLAYER_NAME} sits beside you, staring straight ahead.",
            nextSceneKey = "start_2"
        };

        storyScenes["start_2"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "This court is in session. We are here to adjudicate the case of {PLAYER_NAME}. The charges are grave: illegal medical practice and violation of all ethical norms.",
            nextSceneKey = "start_3"
        };
        
        storyScenes["start_3"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "Mr. Prosecutor, your opening statement.",
            nextSceneKey = "prosecutor_1"
        };

        storyScenes["prosecutor_1"] = new SceneData
        {
            characterName = "Prosecutor",
            portraitKey = "prosecutor",
            // UPDATED: Incorporating the quote about enhancement from the essay draft.
            dialogueText = "Thank you, Your Honor. This case is not just about eliminating disease; it's about the threat of 'enhancement'. As many argue, the deliberate modification of genetic material to 'improve traits' like intelligence or appearance 'is violating the diversity of human species and social stability.'",
            nextSceneKey = "prosecutor_2"
        };

        storyScenes["prosecutor_2"] = new SceneData
        {
            characterName = "Prosecutor",
            portraitKey = "prosecutor",
            // Previous factual element regarding the unauthorized nature.
            dialogueText = "Their actions violated the 2017 international consensus which stated: 'It is irresponsible to proceed with any clinical use of germline editing.' This was an unauthorized leap, not a scientific consensus.",
            nextSceneKey = "defense_1"
        };

        storyScenes["defense_1"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "Mr. Defense Attorney, your statement.",
            nextSceneKey = "defense_2"
        };

        storyScenes["defense_2"] = new SceneData
        {
            characterName = "Lead Defense Attorney",
            portraitKey = "defense",
            dialogueText = "Your Honor, we do not dispute the facts. We dispute the 'intent'. {PLAYER_NAME}'s work aimed at a noble goal: the potential to eliminate latent genetic diseases.",
            nextSceneKey = "defense_3"
        };

        storyScenes["defense_3"] = new SceneData
        {
            characterName = "Lead Defense Attorney",
            portraitKey = "defense",
            dialogueText = "This research was a step—a flawed step, perhaps—toward curing diseases, not designing 'perfect' babies. We will argue this was scientific advancement, not a malicious act.",
            nextSceneKey = "prosecutor_arg_1"
        };

        storyScenes["prosecutor_arg_1"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "Let's proceed to the core arguments. Mr. Prosecutor.",
            nextSceneKey = "prosecutor_arg_2"
        };

        storyScenes["prosecutor_arg_2"] = new SceneData
        {
            characterName = "Prosecutor",
            portraitKey = "prosecutor",
            // Stronger ethical framing based on "playing God" concerns.
            dialogueText = "Your Honor, the societal harms are threefold. First, the religious and moral implications. Surveys show that over 60% of people globally oppose heritable editing, often seeing it as 'playing the role of God.'",
            nextSceneKey = "player_choice_1_prompt"
        };

        storyScenes["player_choice_1_prompt"] = new SceneData
        {
            characterName = "Lead Defense Attorney",
            portraitKey = "defense",
            dialogueText = "(Whispering to you) How should we respond to this? This 'playing God' argument is tricky.",
            choices = new ChoiceData[]
            {
                new ChoiceData { text = "Object! Law, not theology.", points = new PlayerPoints { logic = 1 }, nextSceneKey = "choice_1_logic" },
                new ChoiceData { text = "Let him talk. He sounds emotional.", points = new PlayerPoints { ethics = 1 }, nextSceneKey = "choice_1_ethics" },
                new ChoiceData { text = "Focus on the intent to cure.", points = new PlayerPoints { compassion = 1 }, nextSceneKey = "choice_1_compassion" }
            }
        };

        storyScenes["choice_1_logic"] = new SceneData
        {
            characterName = "Lead Defense Attorney",
            portraitKey = "defense",
            dialogueText = "(Standing) Objection. The court cannot rule on theology. We are a nation of laws, not of one specific faith.",
            nextSceneKey = "judge_ruling_1"
        };

        storyScenes["choice_1_ethics"] = new SceneData
        {
            characterName = "Narrator",
            portraitKey = "prosecutor",
            dialogueText = "You motion for your colleague to wait. The Prosecutor continues, but his voice is rising, his argument becoming more philosophical than legal.",
            nextSceneKey = "judge_ruling_1_alt"
        };

        storyScenes["choice_1_compassion"] = new SceneData
        {
            characterName = "Lead Defense Attorney",
            portraitKey = "defense",
            dialogueText = "(Standing) Objection, Your Honor. The prosecution mischaracterizes the intent. This was about 'curing', an act of compassion, not a theological debate.",
            nextSceneKey = "judge_ruling_1"
        };

        storyScenes["judge_ruling_1"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "Overruled. The court recognizes that profound public and ethical concerns are central to this case and the regulations the defendant allegedly broke. Continue, Prosecutor.",
            nextSceneKey = "prosecutor_arg_3"
        };

        storyScenes["judge_ruling_1_alt"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "Mr. Prosecutor, please stick to the legal and regulatory statutes. While ethical concerns are relevant, this is a court of law. Continue.",
            nextSceneKey = "prosecutor_arg_3"
        };

        storyScenes["prosecutor_arg_3"] = new SceneData
        {
            characterName = "Prosecutor",
            portraitKey = "prosecutor",
            // Used stronger language about social stigmatization.
            dialogueText = "Second, the impact on the disabled community. The very 'language' this science uses—'correcting errors'—sends a message of social stigmatization. It implies that disabled lives are 'not worth living.'",
            nextSceneKey = "player_choice_2_prompt"
        };

        storyScenes["player_choice_2_prompt"] = new SceneData
        {
            characterName = "Lead Defense Attorney",
            portraitKey = "defense",
            dialogueText = "(Whispering) This is a powerful argument. What's our angle?",
            choices = new ChoiceData[]
            {
                new ChoiceData { text = "Argue that preventing suffering IS compassion.", points = new PlayerPoints { compassion = 1 }, nextSceneKey = "choice_2_compassion" },
                new ChoiceData { text = "It helps, not erases, communities.", points = new PlayerPoints { logic = 1 }, nextSceneKey = "choice_2_logic" },
                new ChoiceData { text = "Stay silent. Wait for him to finish.", points = new PlayerPoints { ethics = 1 }, nextSceneKey = "choice_2_ethics" }
            }
        };

        storyScenes["choice_2_compassion"] = new SceneData
        {
            characterName = "Lead Defense Attorney",
            portraitKey = "defense",
            dialogueText = "(Standing) My client was attempting to 'prevent' suffering and disease. Is that not a form of compassion?",
            nextSceneKey = "prosecutor_arg_4"
        };

        storyScenes["choice_2_logic"] = new SceneData
        {
            characterName = "Lead Defense Attorney",
            portraitKey = "defense",
            dialogueText = "(Standing) Your Honor, this technology has the potential to 'help' those with genetic disorders by offering cures, not to erase them as a community.",
            nextSceneKey = "prosecutor_arg_4"
        };

        storyScenes["choice_2_ethics"] = new SceneData
        {
            characterName = "Narrator",
            portraitKey = "prosecutor",
            dialogueText = "You let the prosecutor continue. The point hangs in the air, heavy and unanswered for now.",
            nextSceneKey = "prosecutor_arg_4"
        };

        // --- NEW SECTION: CLASSISM ARGUMENT ---

        storyScenes["prosecutor_arg_4"] = new SceneData
        {
            characterName = "Prosecutor",
            portraitKey = "prosecutor",
            dialogueText = "And finally, Your Honor, we must consider the socio-economic impact. This technology is being enclosed by powerful economic actors.",
            nextSceneKey = "prosecutor_arg_5"
        };

        storyScenes["prosecutor_arg_5"] = new SceneData
        {
            characterName = "Prosecutor",
            portraitKey = "prosecutor",
            // Added a stronger statistic/claim about inequality.
            dialogueText = "It will create a 'genetic classism' where the wealthy can buy enhancements while the poor are left behind. Studies suggest genetic advantage could deepen inequality by up to 20% in one generation.",
            nextSceneKey = "player_choice_classism"
        };

        storyScenes["player_choice_classism"] = new SceneData
        {
            characterName = "Lead Defense Attorney",
            portraitKey = "defense",
            dialogueText = "(Whispering) We need to address this monopoly fear. It resonates with the public.",
            choices = new ChoiceData[]
            {
                new ChoiceData { text = "Propose gene therapies as public goods.", points = new PlayerPoints { compassion = 1, logic = 1 }, nextSceneKey = "defense_classism_public" },
                new ChoiceData { text = "Argue that regulation, not bans, fixes this.", points = new PlayerPoints { logic = 1 }, nextSceneKey = "defense_classism_reg" },
                new ChoiceData { text = "Dismiss it as speculation.", points = new PlayerPoints { ethics = -1 }, nextSceneKey = "defense_classism_dismiss" }
            }
        };

        storyScenes["defense_classism_public"] = new SceneData
        {
            characterName = "Lead Defense Attorney",
            portraitKey = "defense",
            dialogueText = "(Standing) Your Honor, the risk of monopoly is real, but the solution is to treat these therapies as public health goods, subsidized for all, not to ban the science itself.",
            nextSceneKey = "judge_to_defendant"
        };

        storyScenes["defense_classism_reg"] = new SceneData
        {
            characterName = "Lead Defense Attorney",
            portraitKey = "defense",
            dialogueText = "(Standing) Your Honor, this is a regulatory failure, not a scientific one. Strong governance can prevent monopolies. We shouldn't punish the science for the faults of the market.",
            nextSceneKey = "judge_to_defendant"
        };

        storyScenes["defense_classism_dismiss"] = new SceneData
        {
            characterName = "Lead Defense Attorney",
            portraitKey = "defense",
            dialogueText = "(Standing) Your Honor, the Prosecution is speculating about a dystopian future. We deal with the present facts.",
            nextSceneKey = "judge_classism_rebuke"
        };

        storyScenes["judge_classism_rebuke"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "Mr. Attorney, the potential for inequality is a very present fact in medical access. Do not dismiss it so lightly. Proceed.",
            nextSceneKey = "judge_to_defendant"
        };

        // --- END OF NEW SECTION ---

        storyScenes["judge_to_defendant"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "I would like to hear from the defendant. {PLAYER_NAME}, you have heard these arguments. You knew the regulations. Why did you proceed?",
            nextSceneKey = "player_choice_3_prompt"
        };

        storyScenes["player_choice_3_prompt"] = new SceneData
        {
            characterName = "Lead Defense Attorney",
            portraitKey = "defense",
            dialogueText = "(Whispering) This is it. What should they focus on?",
            choices = new ChoiceData[]
            {
                new ChoiceData { text = "Focus on the scientific intentions.", points = new PlayerPoints { logic = 1 }, nextSceneKey = "choice_3_logic" },
                new ChoiceData { text = "Apologize for the secrecy.", points = new PlayerPoints { ethics = 1 }, nextSceneKey = "choice_3_ethics" },
                new ChoiceData { text = "Emphasize the desire to help people.", points = new PlayerPoints { compassion = 1 }, nextSceneKey = "choice_3_compassion" }
            }
        };

        storyScenes["choice_3_logic"] = new SceneData
        {
            characterName = "{PLAYER_NAME}", // The defendant is now the player
            portraitKey = "defendant",
            dialogueText = "Your Honor... I am a scientist. We had a tool, CRISPR, that could change the world. I saw a chance to stop a terrible disease. To... to rewrite the fragile sequences. I just wanted to fix the error.",
            nextSceneKey = "CALCULATE_ENDING" // SPECIAL KEYWORD
        };

        storyScenes["choice_3_ethics"] = new SceneData
        {
            characterName = "{PLAYER_NAME}",
            portraitKey = "defendant",
            dialogueText = "Your Honor... I know that I broke the rules. I know my secrecy was wrong. But I truly believed it was the only way to move the science forward and help people. I am sorry for the deception.",
            nextSceneKey = "CALCULATE_ENDING" // SPECIAL KEYWORD
        };

        storyScenes["choice_3_compassion"] = new SceneData
        {
            characterName = "{PLAYER_NAME}",
            portraitKey = "defendant",
            dialogueText = "Your Honor... I am a scientist, but I am also human. I saw families suffering from genetic diseases. I just wanted to help them. I wanted to... to fix the pain. My belief was in helping humanity.",
            nextSceneKey = "CALCULATE_ENDING" // SPECIAL KEYWORD
        };

        // --- DIFFERENT ENDINGS BASED ON SCORE ---

        // ENDING 1: LOGIC (Focus on Regulation and Science)
        storyScenes["ending_logic_1"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "I have reviewed your arguments. {PLAYER_NAME}, your scientific intent is clear, and the potential of this technology is undeniable.",
            nextSceneKey = "ending_logic_2"
        };
        storyScenes["ending_logic_2"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "However, science cannot exist without law. While we must not stifle innovation, we must bind it with iron-clad international governance. Regulation is the only path forward.",
            nextSceneKey = "ending_logic_3"
        };
        storyScenes["ending_logic_3"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "You are found guilty of procedural violations, but your work will serve as the foundation for a new, regulated era of gene editing. Court Adjourned.",
            nextSceneKey = "end_score" // Now points to the score page
        };

        // ENDING 2: ETHICS (Focus on Morality and Society)
        storyScenes["ending_ethics_1"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "{PLAYER_NAME}, your plea for your secrecy is noted. But secrecy in such matters is not just a procedural error; it is a moral failing.",
            nextSceneKey = "ending_ethics_2"
        };
        storyScenes["ending_ethics_2"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "We are not just rewriting DNA; we are rewriting the social contract. To 'play God' without the consent of society is an act of hubris we cannot tolerate.",
            nextSceneKey = "ending_ethics_3"
        };
        storyScenes["ending_ethics_3"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "You are found guilty. Let this verdict send a message: Ethical boundaries are not suggestions. They are the walls that protect our humanity. Court Adjourned.",
            nextSceneKey = "end_score" // Now points to the score page
        };

        // ENDING 3: COMPASSION (Focus on Access and Suffering)
        storyScenes["ending_compassion_1"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "{PLAYER_NAME}, your plea for the suffering of families moves this court. The desire to heal is the highest calling of medicine.",
            nextSceneKey = "ending_compassion_2"
        };
        storyScenes["ending_compassion_2"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "But true compassion requires justice. If these cures become available only to the rich, we have failed. We must ensure these therapies are public goods, accessible to all.",
            nextSceneKey = "ending_compassion_3"
        };
        storyScenes["ending_compassion_3"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "You are found guilty of bypassing safety protocols, but your motivation highlights our urgent need for an inclusive healthcare system. Court Adjourned.",
            nextSceneKey = "end_score" // Now points to the score page
        };
        
        // ENDING 4: BALANCED/DEFAULT
        storyScenes["ending_balanced_1"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "This case has exposed the deep complexity of our future. We have heard arguments of science, faith, and justice.",
            nextSceneKey = "ending_balanced_2"
        };
        storyScenes["ending_balanced_2"] = new SceneData
        {
            characterName = "Judge",
            portraitKey = "judge",
            dialogueText = "We must find a middle path. We must balance innovation with ethical responsibility. Global governance is no longer optional; it is a necessity.",
            nextSceneKey = "end_score" // Now points to the score page
        };

        // --- SCENE FOR SCORE ---
        storyScenes["end_score"] = new SceneData
        {
            characterName = "Narrator",
            portraitKey = "none",
            dialogueText = "The trial ends. Your Final Score - Logic: {LOGIC}, Ethics: {ETHICS}, Compassion: {COMPASSION}. This score has been saved.",
            nextSceneKey = "end_final" // Points to the final thank you screen
        };
        // --- END NEW SCENE ---


        storyScenes["end_final"] = new SceneData
        {
            characterName = "Narrator",
            portraitKey = "none",
            dialogueText = "Thank you for playing.", // Only the final message
            nextSceneKey = null // No next scene
        };
    }

    // This function updates the UI based on the current scene
    void RenderScene()
    {
        SceneData scene = storyScenes[currentSceneKey];

        // Replace placeholders with actual values
        string finalDialogue = scene.dialogueText.Replace("{PLAYER_NAME}", playerName);
        string finalCharName = scene.characterName.Replace("{PLAYER_NAME}", playerName);

        // Check for the new score intro scene and replace previous score placeholders
        if (currentSceneKey == "score_display_intro" && hasPreviousScore)
        {
            finalDialogue = finalDialogue.Replace("{PREV_LOGIC}", previousScore.logic.ToString());
            finalDialogue = finalDialogue.Replace("{PREV_ETHICS}", previousScore.ethics.ToString());
            finalDialogue = finalDialogue.Replace("{PREV_COMPASSION}", previousScore.compassion.ToString());
        }

        // Check for the final score scene
        if (currentSceneKey == "end_score") 
        {
            finalDialogue = finalDialogue.Replace("{LOGIC}", playerPoints.logic.ToString());
            finalDialogue = finalDialogue.Replace("{ETHICS}", playerPoints.ethics.ToString());
            finalDialogue = finalDialogue.Replace("{COMPASSION}", playerPoints.compassion.ToString());
        }

        // Update UI elements
        characterNameText.text = finalCharName;
        dialogueText.text = finalDialogue;
        
        // Update the visual portrait based on the key
        if (portraitVisual != null)
        {
            Sprite newSprite = portraitSprites[scene.portraitKey];
            portraitVisual.sprite = newSprite;
            // Only hide the container if the portrait is null
            characterPortraitContainer.SetActive(newSprite != null);

            // --- DYNAMIC ASPECT RATIO CALCULATION AND SCALING ---
            if (aspectRatioFitter != null && newSprite != null)
            {
                // 1. Set Aspect Ratio dynamically based on the sprite's dimensions
                float ratio = newSprite.rect.width / newSprite.rect.height;
                aspectRatioFitter.aspectRatio = ratio;

                // 2. Set Scale dynamically (Narrator gets bigger)
                if (portraitContainerRectTransform != null)
                {
                    if (scene.portraitKey == "none")
                    {
                        // Make the narrator image NARRATOR_SCALE_FACTOR times larger than normal
                        portraitContainerRectTransform.localScale = originalScale * NARRATOR_SCALE_FACTOR; 
                    }
                    else
                    {
                        // Restore the original scale for all character portraits
                        portraitContainerRectTransform.localScale = originalScale;
                    }
                }
            }
            // --- END DYNAMIC ASPECT RATIO CALCULATION AND SCALING ---
        }
        else
        {
            // Fallback for debugging, if the child Image wasn't found
            characterPortraitContainer.SetActive(false);
        }

        // Clear old choice buttons
        foreach (Transform child in choiceContainer)
        {
            Destroy(child.gameObject);
        }

        // Check if there are choices
        if (scene.choices != null && scene.choices.Length > 0)
        {
            choiceContainer.gameObject.SetActive(true);

            // Create new choice buttons
            foreach (ChoiceData choice in scene.choices)
            {
                GameObject buttonGO = Instantiate(choiceButtonPrefab, choiceContainer);
                buttonGO.GetComponentInChildren<TextMeshProUGUI>().text = choice.text;
                
                // Add a listener to the button to call HandleChoice when clicked
                Button button = buttonGO.GetComponent<Button>();
                button.onClick.AddListener(() => HandleChoice(choice));
            }
        }
        else
        {
            choiceContainer.gameObject.SetActive(false);
        }
    }

    // Called when a choice button is clicked
    void HandleChoice(ChoiceData choice)
    {
        // Add points
        playerPoints.logic += choice.points.logic;
        playerPoints.ethics += choice.points.ethics;
        playerPoints.compassion += choice.points.compassion;

        // Check for special ending calculation
        if (choice.nextSceneKey == "CALCULATE_ENDING")
        {
            SaveCurrentScore(); // NEW: Save score before ending
            CalculateAndLoadEnding();
        }
        else
        {
            // Go to the next scene normally
            currentSceneKey = choice.nextSceneKey;
            RenderScene();
        }
    }

    // Called by the EventTrigger on the DialoguePanel
    public void OnDialogueBoxClick()
    {
        // <--- NEW: Handle click from Title Scene to move to Name Entry Panel --->
        if (currentSceneKey == "title")
        {
            StartGame();
            return;
        }
        // <--- END NEW --->
        
        SceneData scene = storyScenes[currentSceneKey];

        // Only advance if there are no choices and it's not the end
        if ((scene.choices == null || scene.choices.Length == 0) && scene.nextSceneKey != null)
        {
            if (scene.nextSceneKey == "CALCULATE_ENDING")
            {
                SaveCurrentScore(); // NEW: Save score before ending
                CalculateAndLoadEnding();
            }
            else
            {
                currentSceneKey = scene.nextSceneKey;
                RenderScene();
            }
        }
    }

    // Helper function to decide which ending to show
    void CalculateAndLoadEnding()
    {
        // Simple logic to determine highest score
        if (playerPoints.logic > playerPoints.ethics && playerPoints.logic > playerPoints.compassion)
        {
            currentSceneKey = "ending_logic_1";
        }
        else if (playerPoints.ethics > playerPoints.logic && playerPoints.ethics > playerPoints.compassion)
        {
            currentSceneKey = "ending_ethics_1";
        }
        else if (playerPoints.compassion > playerPoints.logic && playerPoints.compassion > playerPoints.ethics)
        {
            currentSceneKey = "ending_compassion_1";
        }
        else
        {
            // Default/Balanced ending if scores are tied
            currentSceneKey = "ending_balanced_1";
        }

        RenderScene();
    }
}