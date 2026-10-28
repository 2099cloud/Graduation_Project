INCLUDE 1st scene.ink
INCLUDE 2nd scene.ink
INCLUDE 3&4 scene.ink
INCLUDE 5th scene.ink
INCLUDE 7th scene.ink
INCLUDE 9th scene.ink
INCLUDE 10th-1 scene.ink
INCLUDE 10th-2 scene.ink
INCLUDE 10th-3 scene.ink
INCLUDE 11th-1 scene.ink
INCLUDE 11th-2 scene.ink
INCLUDE 11th-3 scene.ink
INCLUDE InspectableObject.ink

EXTERNAL IsVerified(objectID)
EXTERNAL GetTotalScore()
VAR scene = 1

//-----Plot-----
=== prologue ===
.#bgm:CHEAT4



.#Old_Brother.SetActive.false
.#Laptop.SetActive.true
.#LightingData_Dusk_nolight

.#Animation.timeline.0
.#phone.mode.Interactor

->prologue_settings

=== prologue_settings ===
//door knot settings
.#door.TargetKnot="door"
.#door001.TargetKnot="door001"
.#door002.TargetKnot="door002"
.#Front_door.TargetKnot="Front_door"
.#Table.TargetKnot="Story0_1"
->DONE




===Story0_1===
.# Main Character.position(-5.8448596,-0.0159999132,-1.41833425)
.# Main Character.rotation(0,355.446,0)
.#Main Character.SetActive.false
.#Table.TargetKnot=" "
.#Virtual Camera(1P).SetActive.true
.#FreeLook Camera(3P).SetActive.false
.#phone.isInteractable.true
.#passbook.isInspectable.true
.#CCIR.isInspectable.true
.#Close_1P.SetActive.true
.# timer.30.ringtone
//.#Animation.timeline.0-1
.
->DONE


===ringtone===
.#phone.prefab.phone_call
.# sfx: ringtone.loop
->DONE




===Story0_1_close===
.#Main Character.SetActive.true
.#Table.TargetKnot="Story0_1"
.#Close_1P.SetActive.false
#FreeLook Camera(3P).SetActive.true
#Virtual Camera(1P).SetActive.false

.#phone.isInteractable.false
.#passbook.isInspectable.false
.#CCIR.isInspectable.false
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
    .#Close_1P.SetActive.false
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
.#Animation.timeline.0-2
->DONE

=== proof ===
//請選擇要提出的證據：
    // 只有當 "Notebook" 完全 verify 時才會顯示這個選項
    { IsVerified("package"):
        + [包裹] -> proof_package
    }
    
    // 只有當 "Passbook" 完全 verify 時才會顯示這個選項
    { IsVerified("Passbook"):
        + [存摺] -> proof_passbook
    }
    
    // 只有當 "StickyNote" 完全 verify 時才會顯示這個選項
    { IsVerified("StickyNote"):
        + [便利貼] -> proof_stonote
    }
    
    // 保險起見，加入一個防呆選項（如果玩家什麼都沒 verify，避免卡死）
    + [暫時不出示證據] -> no_proof

=== proof_package ===
拿出筆記...
-> END

=== proof_passbook ===
拿出存摺...
-> END

=== proof_stonote ===
拿出便利貼...
-> END

=== no_proof ===
決定再仔細想想...
-> END



=== check_ending1 ===
~ temp current_score = GetTotalScore()

// 根據分數決定走向
{ current_score >= 80:
    -> Story10_1
- else:
    { current_score >= 50:
        -> Story10_2
    - else:
        -> Story10_3
    }
}

=== check_ending2 ===
~ temp current_score = GetTotalScore()

// 根據分數決定走向
{ current_score >= 80:
    -> Story11_1
- else:
    { current_score >= 50:
        -> Story11_1
    - else:
        -> Story11_1
    }
}
//--------------------------------------------------
##Syntax##


# TimelineSignal(2)
#OpenTutorial[0, 2]
# sfx: stop
# sfx: fadeout.2.5
# bgm: stop
# bgm: fadeout.2.0
# Desk.isInspectable.true
# SafeBox.isInspectable.false
#delay.2.0//延遲兩秒
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
# bgm: AlleyTheme
# sfx: BrowserClick.loop
這是一段開場對白，當進入這個節點時，會自動去 Resources 載入並播放特定的 Timeline。
NPC點點頭說道：「你終於來了。」
註：DirectorObject 是你場景上掛載 PlayableDirector 的物件名稱，Timeline_IntroScene 則是對應的 Timeline 資源檔名。