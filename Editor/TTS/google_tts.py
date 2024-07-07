import os
import json
import subprocess
import argparse
import sys
import time
from google.cloud import texttospeech

def check_pipes():
    if sys.platform == 'win32':
        print("Running on Windows")
        toname = r'\\.\pipe\ToSrvPipe'
        fromname = r'\\.\pipe\FromSrvPipe'
        eol = '\r\n\0'
    else:
        print("Running on Linux or Mac")
        toname = f'/tmp/audacity_script_pipe.to.{os.getuid()}'
        fromname = f'/tmp/audacity_script_pipe.from.{os.getuid()}'
        eol = '\n'

    print("Write to  \"" + toname + "\"")
    # if not os.path.exists(toname):
    #     raise FileNotFoundError("Named pipe for writing does not exist. Ensure Audacity is running with mod-script-pipe.")
        
    # print("Read from \"" + fromname + "\"")
    # if not os.path.exists(fromname):
    #     raise FileNotFoundError("Named pipe for reading does not exist. Ensure Audacity is running with mod-script-pipe.")

    try:
        tofile = open(toname, 'w')
        print("-- File to write to has been opened")
        fromfile = open(fromname, 'rt')
        print("-- File to read from has now been opened too\r\n")
    except Exception as e:
        print(f"Failed to open pipes: {e}")
        raise

    return tofile, fromfile, eol

def send_command(command, tofile, eol):
    """Send a single command."""
    print("Send: >>> \n"+command)
    tofile.write(command + eol)
    tofile.flush()

def get_response(fromfile):
    """Return the command response."""
    result = ''
    line = ''
    while True:
        result += line
        line = fromfile.readline()
        if line == '\n' and len(result) > 0:
            break
    return result

def do_command(command, tofile, fromfile, eol):
    """Send one command, and return the response."""
    send_command(command, tofile, eol)
    response = get_response(fromfile)
    print("Rcvd: <<< \n" + response)
    return response

def synthesize_speech(text, output_file, client, voice, audio_config):
    """Synthesizes speech from the input string of text and saves it to the output file."""
    
    # Ensure the output directory exists
    os.makedirs(os.path.dirname(output_file), exist_ok=True)

    input_text = texttospeech.SynthesisInput(text=text)
    
    response = client.synthesize_speech(
        input=input_text, voice=voice, audio_config=audio_config
    )

    # Write the response to the output file.
    with open(output_file, "wb") as out:
        out.write(response.audio_content)
        print(f"Audio content written to file {output_file}")

def apply_audacity_macro(input_file, macro_name, tofile, fromfile, eol):
    """Applies an Audacity macro to the input file using mod-script-pipe."""
    input_file = input_file.replace("\\", "/")

    import_command = f'Import2: Filename={input_file}'
    do_command(import_command, tofile, fromfile, eol)
    
    select_command = 'SelectAll:'
    do_command(select_command, tofile, fromfile, eol)
    
    macro_command = f'Macro_{macro_name}'
    do_command(macro_command, tofile, fromfile, eol)
    
    export_command = f'Export2: Filename={input_file}'
    do_command(export_command, tofile, fromfile, eol)

    delete_command = f'RemoveTracks'
    do_command(delete_command, tofile, fromfile, eol)
    
    print(f"Applied Audacity macro {macro_name} to {input_file}")

def get_gender_voice(gender):
    gender = gender.lower()
    if gender == "male":
        return texttospeech.SsmlVoiceGender.MALE
    elif gender == "female":
        return texttospeech.SsmlVoiceGender.FEMALE
    else:
        return texttospeech.SsmlVoiceGender.NEUTRAL

def main():
    parser = argparse.ArgumentParser(description="Google TTS API example script.")
    parser.add_argument("--file", type=str, help="JSON file containing text to synthesize.")
    parser.add_argument("--text", type=str, help="Text to synthesize.")
    parser.add_argument("--key", type=str, help="Key for the text entry.")
    parser.add_argument("--locale", type=str, default="en", help="Locale for the text entry.")
    parser.add_argument("--gender", type=str, default="female", help="Locale for the text entry.")
    parser.add_argument("--output_dir", type=str, required=True, help="Output directory for audio files.")
    parser.add_argument("--credentials", type=str, required=True, help="Path to Google Cloud service account key JSON file.")
    parser.add_argument("--macro_name", type=str, help="Name of the Audacity macro to apply (optional).")
    
    args = parser.parse_args()

    # Initialize TTS client with explicit credentials
    client = texttospeech.TextToSpeechClient.from_service_account_file(args.credentials)

    audio_config = texttospeech.AudioConfig(
        audio_encoding=texttospeech.AudioEncoding.MP3
    )

    # If Audacity macro provided, setup the named pipes
    if args.macro_name:
        tofile, fromfile, eol = check_pipes()

    if args.file:
        # Read the JSON file and process each entry
        with open(args.file, "r", encoding="utf-8") as json_file:
            localization_data = json.load(json_file)
            for entry in localization_data['entries']:
                for locale, text in entry['texts'].items():
                    if text['regenerate'] == True:
                        # Set the voice parameters
                        voice = texttospeech.VoiceSelectionParams(
                            language_code=locale,
                            ssml_gender=get_gender_voice(args.gender)
                        )
                        
                        output_file = os.path.join(args.output_dir, locale, f"{entry['key']}_{locale}.mp3")
                        print("Generating audio for key ", entry['key'])
                        synthesize_speech(text["value"], output_file, client, voice, audio_config)
                        if args.macro_name:
                            apply_audacity_macro(output_file, args.macro_name, tofile, fromfile, eol)
    elif args.text and args.key:
        # Process the single text entry from the command line
        voice = texttospeech.VoiceSelectionParams(
            language_code=args.locale,
            ssml_gender=get_gender_voice(args.gender)
        )

        output_file = os.path.join(args.output_dir, locale, f"{args.key}_{args.locale}.mp3")
        synthesize_speech(args.text, output_file, client, voice, audio_config)
        if args.macro_name:
            apply_audacity_macro(output_file, args.macro_name, tofile, fromfile, eol)
    else:
        print("Please provide either a JSON file with --file or both --text and --key arguments.")
        return

if __name__ == "__main__":
    main()
