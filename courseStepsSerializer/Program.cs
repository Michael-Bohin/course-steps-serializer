
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using static System.Console;
using System.Net.Http.Headers;
/** Course step types:
* 
* text:		txt, string title, int duration, string text
* 
* video:		vid, string title, int duration, string URL
* 
* kviz:		now substitue with text
* 
* exercise:	exe
* 
*/

JsonSerializerOptions options = new() {
	Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
	WriteIndented = true,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

CourseStepParser csp = new(options);
csp.Run();

class CourseStepParser {
	int stepId = 1;
	int sectionId = 1;
	readonly List<SectionMetaData> sectionsMetaData = new();
	CourseJson course = new();
	JsonSerializerOptions options;

	public CourseStepParser(JsonSerializerOptions options) {
		this.options = options;
		course.slug = "priprava-na-prijimacky";
		course.title = "Příprava na přijímačky";
		course.description = "Online kurz pro deváťáky. Příprava na přijímací zkoušky z matematiky do středních škol.";
		course.grade = 9;
		course.language= "cs";

		using StreamReader sr = new("../../../in/sectionsInfo.csv");
		while (sr.ReadLine() is string line) {
			string[] splitLine = line.Split(';');
			if(splitLine.Length != 3) {
				throw new ArgumentException("Section meta data must contain three columns");
			}
			
			string folderName = splitLine[0];
			string slug = splitLine[1];
			string title = splitLine[2];

			SectionMetaData smd = new(sectionId, folderName, slug, title);
			sectionId++; // >>> Increment section ID <<<
			sectionsMetaData.Add(smd);
		}
	}

	public void Run() {
		// pro kazdy IDF
		//		otevri textax se jmenem $"{sectionIDF}-courseSteps.txt"
		//		reprezentci v souboru si hod do List<Step>
		//		vytvor si instanci section (zde se pozdeji dodaji info k dane section)
		//		prirad steps
		//		prirad do course  
		//	
		// zasarializuj course do jsonu a napisu do ./out/course.json
		// 

		foreach(SectionMetaData smd in sectionsMetaData) { 
			StreamReader sr = new($"../../../in/{smd.folderName}-courseSteps.csv");	
			List<Step> sectionSteps = new();
			while(sr.ReadLine() is string line) {
				if (line.Trim() == "") 
					continue; // skip empty lines, so that I can indent the input csv documents.

				string[] splitLine = line.Split(';');
				Step step = ParseStepFromCSVLine(splitLine);
				sectionSteps.Add(step);
				stepId++; // >>> Increment step ID <<<
				
			}

			Section section = new Section(smd.SectionId, smd.slug, smd.title, sectionSteps);
			course.sections.Add(section);
		}

		string outJson = JsonSerializer.Serialize(course, options);
		using StreamWriter sw = new StreamWriter("./out/course.json");
		sw.WriteLine(outJson);
	}

	Step ParseStepFromCSVLine(string[] splitLine) {
		if (splitLine[0] == "vid") {
			return ParseVideoLine(splitLine);
		} 
		
		if(splitLine[0] == "txt") {
			return ParseTextLine(splitLine);
		} 
		
		if(splitLine[0] == "pretest") {
			return ParsePretestLine(splitLine);
		} 
		
		if(splitLine[0] == "exe") {
			return ParseExerciseLine(splitLine);
		}

		throw new ArgumentException("csv line contains unknown IDF: " + splitLine[0]);
	} 

	Step ParseVideoLine(string[] splitLine) {
		if (splitLine.Length < 4 || 5 < splitLine.Length ) {
			throw new ArgumentException("Video line must be parsed into 4 parts.");
		}

		// length 5 -> slitLine[5] contains bool askForFeedback -> must be in form of string "true", else throw ArgumentExcepetion

		string title  = splitLine[1];
		int duration = int.Parse(splitLine[2]);
		string URL = splitLine[3];

		Step step = new() {
			stepId = stepId,
			title = title, 
			duration = duration, 
			video = URL 
		};

		if(splitLine.Length == 5) {
			string ask = splitLine[4];
			if(ask != "askForFeedback") {
				throw new ArgumentException("Video's fifth column must be etheir nonexistent or hold value true. Value deteceted: " + ask);
			}

			step.askForFeedback = true;
		}

		return step;
	}

	Step ParseTextLine(string[] splitLine) {
		if (splitLine.Length != 4) {
			throw new ArgumentException("Video line must be parsed into 4 parts.");
		}

		string title = splitLine[1];
		int duration = int.Parse(splitLine[2]);
		string text = splitLine[3];

		Step step = new() {
			stepId = stepId,
			title = title,
			duration = duration,
			text = text
		};

		return step;
	}

	Step ParsePretestLine(string[] splitLine) {
		if (splitLine.Length <= 7) {
			throw new ArgumentException("Pretest line must be parsed into at least 8 parts.");
		}

		string title = splitLine[1];
		int duration = int.Parse(splitLine[2]);
		int stepsCount = int.Parse(splitLine[3]);
		string language = splitLine[4];
		int exercisesCount = int.Parse(splitLine[5]);


		if (stepsCount < 1) { 
			throw new ArgumentException("There must be at least one step in pretest line!");	
		}

		List<int> steps = new();
		int tempStep = stepId;
		for(int i = 0; i < stepsCount; i++) {
			tempStep++;
			steps.Add(tempStep);
		}
		
		if (language != "cs") {
			throw new ArgumentException("Only Czech exercises are expected at this point.");
		}

		if(exercisesCount < 1) {
			throw new ArgumentException("A quiz determining level of knowledge of kids should have at least one exercise. I dont want to buly empty set, but how can empty set of exercises test anyone?");
		}

		List<ExerciseID> exercises = new();	

		for(int i = 6; i < (6 + 2*exercisesCount); i += 2) {
			int id = int.Parse(splitLine[i]);
			int count = int.Parse(splitLine[i+1]);
			exercises.Add(new(id, count));
		}

		Exercise exercise = new(language, exercises);

		Pretest pretest = new() {
			steps = steps,
			exercise = exercise
		};

		Step step = new() {
			title = title,
			duration = duration,
			stepId = stepId,
			pretest = pretest
		};

		return step;
	}

	Step ParseExerciseLine(string[] splitLine) {
		if (splitLine.Length < 6) {
			throw new ArgumentException("Video line must be parsed into at least 6 parts.");
		}

		string title = splitLine[1];
		int duration = int.Parse(splitLine[2]);
		string language = splitLine[3];
		if(language != "cs") {
			throw new ArgumentException("Only Czech exercises are expected at this point.");
		}
		Step step = new() {
			stepId = stepId,
			title = title,
			duration = duration
		};

		List<ExerciseID> exercises = ParseExerciseIDs(splitLine);
		
		step.exercise = new Exercise(language, exercises);

		return step;
	}

	List<ExerciseID> ParseExerciseIDs(string[] splitLine) {
		List<ExerciseID> exercises = new();
		int idIndex = 4;
		int length = splitLine.Length;

		foreach(string line in splitLine) {
			Write(line + ' ');
		}
		WriteLine();

		while(idIndex < length) {
			string strId = splitLine[idIndex];
			int id = int.Parse(strId);

			string strCount = splitLine[idIndex + 1];
			int count = int.Parse(strCount);

			exercises.Add(new(id, count));
			idIndex += 2;
		}

		return exercises;
	}
}



/// <summary>
/// /////////////////////////////////////////
/// </summary>
/// 
public class CourseJson {
	public string slug { get; set; }
	public string title { get; set; }
	public string description { get; set; }
	public int grade { get; set; }
	public string language { get; set; }
	public List<Section> sections { get; set; } = new();
}

public class Section {
	public int sectionId { get; set; }
	public string slug { get; set; }
	public string title { get; set; }
	public List<Step> steps { get; set; }

	public Section(int sectionId, string slug, string title, List<Step> steps) {
		this.sectionId = sectionId;
		this.slug = slug;
		this.title = title;
		this.steps = steps;
	}
}

public class Step {
	public int stepId { get; set; }
	public string title { get; set; }
	public int duration { get; set; }
	public string? video { get; set; }
	public bool? askForFeedback { get; set; }
	public Exercise? exercise { get; set; }
	public string? text { get; set; }
	public Pretest? pretest { get; set; }
}

public class Exercise {
	public string language { get; set; }
	public List<ExerciseID> exercises { get; set; } = new();

	public Exercise(string language, List<ExerciseID> exercises) {
		this.language = language;
		this.exercises = exercises;
	}
}

public class Pretest {
	public List<int> steps { get; set; } = new();
	public Exercise? exercise { get; set; } 
}

public class ExerciseID {
	public int exerciseId { get; set; }
	public int count { get; set; }

	public ExerciseID(int exerciseId, int count) {
		this.exerciseId = exerciseId;
		this.count = count;
	}
}



/// 

class SectionMetaData {
	public int SectionId { get; set; }
	public string slug { get; set; }
	public string title { get; set; }
	public string folderName { get; set; }
	public SectionMetaData(int sectionId, string folderName, string slug, string title) {
		this.SectionId = sectionId;
		this.slug = slug;
		this.title = title;
		this.folderName = folderName;
	}
}
	
