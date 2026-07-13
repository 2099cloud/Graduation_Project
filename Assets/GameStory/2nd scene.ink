=== Scene2_start ===
.# phone.prefab.smile
.#phone.TargetKnot="Test"
//.#Area Light.prefab.Yellow
.#Front door.TargetKnot="Answer_the_door"
.#Task.text.應門

-> Task_Assignment


=== Play_Animation ===
 // 注意這裡前面有一個空格，用來帶出標籤
 # phone.timeline.incoming_ring
 
 // 播完動畫後，鎖定全場景點擊，逼玩家專心看演出
 # InteractableObject.non-interactable
-> DONE

=== Task_Assignment ===
 // 派發任務：讀取應門項目
//.#Task.text.應門
-> DONE

=== Answer_the_door===
+[應門?]
.#fade
->Receive_package
+[不理會]
->DONE

=== Receive_package ===
.# Main Character.position(-7.83, 0.345, 5.95)
.# Main Character.rotation(0, 0, 0)
 外送員站在門口。
 END
-> DONE

=== Test ===
這是您的包裹，請您簽收
+[接收包裹]
你收到了一個包裹，上面印有...
.#Courier.SetActive.false
.#Task.text.檢查包裹

-> DONE

=== fade ===
.#fade
->DONE


//--------------------------------------------------
=== Choice1 ===
+[哥，這有你的包裹。(待修改)]
->Answer.Answer1
+[哥，這包裹寄件人寫得怪怪的。]
->Answer.Answer1
+[哥，你是不是又亂買奇奇怪怪的東西。]
->Answer.Answer1

=== Choice2 ===
+[網購筆電嗎？是在哪買的啊?(待修改)]
->Answer.Answer2
+[便宜的筆電？你確定這來源沒問題嗎？]
->Answer.Answer2
+[這顯然是假的，網上便宜筆電都是騙人的。]
->Answer.Answer2

=== Choice3 ===
+[我最近看新聞，很多網購筆電都是騙局。(待修改)]
->Answer.Answer3
+[廣告買的？這種一頁式的的通常不太安全耶。]
->Answer.Answer3
+[你怎麼會上當？社群廣告全都是詐騙。]
->Answer.Answer3

=== Choice4 ===
+[如果是真的，能不能也讓我看看規格？(待修改)]
->Answer.Answer4
+[你有確定過這個賣家真的是官方的嗎？]
->Answer.Answer4
+[你被騙了，這包裝看起來就像是詐騙包裹。]
->Answer.Answer4

=== Choice5 ===
+[那不然我先幫你收著，你等一下再確認吧?(待修改)]
->Answer.Answer5
+[...我覺得你還是先別拆，晚點再查一下這家公司比較好。]
->Answer.Answer5
+[還是乾脆別拆了，我直接拿去退貨給快遞吧。]
->Answer.Answer5

===Answer===

=Answer1
哥哥：喔，那是我在網路上買的新筆電，很便宜。
->Choice2

=Answer2
哥哥：就社群廣告看到的，看到有優惠，性能又還不錯就買下來了。
->Choice3

=Answer3
哥哥：反正能用就好了，別想那麼多。
->Choice4

=Answer4
哥哥：好了啦，我有檢查過了，我現在很忙，晚點再說。
->Choice5

=Answer5
哥哥：沒事啦，我自己清楚我買了甚麼，你不要想那麼多啦。
哥哥：我要繼續忙了，你先出去吧。
->DONE
