=== Story1_1 ===
.# sfx: fadeout.2.5
.#bgm:CHEAT1
.#Skybox.Day
.#LightingData_Day
.#Main Character.SetActive.true
.#Animation.timeline.1-1
.#1P Camera.SetActive.false
.#FreeLook Camera(3P).SetActive.true

->Scene1_Settings

===Scene1_Settings===
.#Task.text.應門
.#door002.TargetKnot=""
.#Front_door.TargetKnot="Story1_2"
.#OpenTutorial(0)
->DONE

=== Story1_2 ===
.#Main Character.SetActive.true
.#Animation.timeline.1-2
快遞：您好，有您的包裹。
弟弟：(不是我買的...是哥訂的嗎?)
弟弟：哦，好，謝謝，放著就可以了。
快遞：這邊跟您收三千元。
弟弟：(價格還真高...是買了甚麼?)
弟弟：好，給你。
快遞：好的，謝謝。

->Check_Package

=== Check_Package ===
.#Task.text.檢查包裹
.#fade.In
->DONE
