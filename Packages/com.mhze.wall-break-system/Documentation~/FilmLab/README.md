# Film lab: test and film a wall break without the shared editor

How the break was developed and filmed (Unity 6000.6, URP). It needs no scene and no player rig, so it runs in a scratch project
beside the game while other agents keep the main editor busy.

1. **Scratch project.** Make a folder with a short path (a long path breaks URP's editor assets), for example `C:\Users\<you>\Projects\Unity\_wt\wall-lab`.
   Copy `ProjectSettings/` from any working Unity 6 project and set `ProjectVersion.txt` to your editor version.
   `Packages/manifest.json`:

   ```json
   {
     "dependencies": {
       "com.mhze.wall-break-system": "file:C:/path/to/Packages/com.mhze.wall-break-system",
       "com.unity.render-pipelines.universal": "17.6.0",
       "com.unity.test-framework": "1.8.0",
       "com.unity.modules.animation": "1.0.0",
       "com.unity.modules.audio": "1.0.0",
       "com.unity.modules.imageconversion": "1.0.0",
       "com.unity.modules.particlesystem": "1.0.0",
       "com.unity.modules.physics": "1.0.0",
       "com.unity.modules.screencapture": "1.0.0"
     },
     "testables": [ "com.mhze.wall-break-system" ]
   }
   ```

2. **Files.** Put `LabSetup.cs` in `Assets/Editor/` (it makes a URP pipeline asset and makes it the default) and
   `WallMovieTests.cs` plus `WallLab.PlayTests.asmdef` in `Assets/Tests/`.
3. **Compile once** so the script cache is complete (otherwise the test assembly can compile with no references):
   `Unity.exe -batchmode -nographics -quit -projectPath <lab> -executeMethod LabSetup.Ensure -logFile compile.log`
4. **Film.** Set `WALL_FRAMES` to an output folder, then run the play-mode test *with graphics* (no `-nographics`):
   `Unity.exe -batchmode -projectPath <lab> -runTests -testPlatform PlayMode -testResults results.xml -logFile play.log`.
   It builds a brick wall, a floor and two cameras, strikes the wall six times and writes `A_nnnn.jpg` (player side) and
   `B_nnnn.jpg` (behind the wall) for every frame at a fixed 30 fps.
   - **Never start a second Unity on the same lab while one is still running**: it exits at once and the frame folder gets frames from both runs.
   - A test cannot `yield return new WaitForEndOfFrame()` in batch mode; the test reads each frame's render texture one frame later.
5. **Contact sheets and video.** `make_comp.py` (needs `opencv-python`) joins the two cameras side by side, labels them and adds a slow-motion
   replay of the break. Encode with any ffmpeg: `ffmpeg -framerate 30 -i comp/f_%05d.jpg -c:v libx264 -crf 20 -pix_fmt yuv420p out.mp4`.
   (Blender 5 here has no ffmpeg output and this machine's OpenCV has no H.264 encoder; `pip install --target <folder> imageio-ffmpeg`
   gives an ffmpeg binary without touching the system Python.)
6. **Look at the frames.** The numbers in the log (pieces, resting) say nothing about how it looks; open sample frames around the last
   blow and check that the doorway is open and the pieces really fell.
