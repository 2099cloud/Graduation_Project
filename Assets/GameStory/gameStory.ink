INCLUDE 1st scene.ink
INCLUDE 2nd scene.ink
INCLUDE 9th scene.ink

VAR scene = 1

//-----Plot-----
=== prologue ===
//.#Main Character.SetActive.false
.#Old_Brother.SetActive.false
.#LightingData_Night

.#Animation.timeline.0
.#phone.mode.Interactor

->DONE

===evidence===
#FreeLook Camera(3P).SetActive.false
.#Animation.timeline.0-1
->DONE

===Repeat===

=Sentence1
我想再看看那些東西。
->DONE
=Sentence2
當時，是不是還有甚麼我能做的?
->DONE



//-----function-----

=== PickUpthePhone ===
+[接聽] 
    //# PhoneAction: PickUp
    //# ChangePhoneImage: Incoming_Call
    .#Animation.timeline.0-2
    -> Story1_1

+[掛斷]
    -> DONE
    
===door===
我跟他共用電腦的書房…。
或許我早就該多上網了解一下那種傢伙的做案手法的。
END
->DONE

===door001=== 
這是他的房間。
我當時應該阻止他的。
END
->DONE

===door002=== 
我不想躲回房間...什麼都不做只會讓一切更糟糕。
END
->DONE

===Front_door=== 
現在不是出門的時候。
END
->DONE
===test===
.#Task.text.紙箱
.#door.TargetKnot=""
-> DONE



//--------------------------------------------------
##Syntax##

#phone.prefab.smile
#phone.prefab.incomingcall
#phone.TargetKnot="Test"
#Front door.TargetKnot="Answer_the_door"
# DeskBox.mode.Inspector
#fade
# Main Character.position(-7.83, 0.345, 5.95)
# Main Character.rotation(0, 0, 0)
#Task.text.應門
# DirectorObject.timeline.Timeline_IntroScene
這是一段開場對白，當進入這個節點時，會自動去 Resources 載入並播放特定的 Timeline。
NPC點點頭說道：「你終於來了。」
註：DirectorObject 是你場景上掛載 PlayableDirector 的物件名稱，Timeline_IntroScene 則是對應的 Timeline 資源檔名。