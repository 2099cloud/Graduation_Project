===Scene5_Settings===
#Old_Brother.TargetKnot="Story5"
#Old_Brother.position(-7.83, 0.345, 5.95)
#Old_Brother.rotation(0, 0, 0)
->DONE

=== Story5 ===
.#LightingData_Dusk
.#Animation.timeline.5
哥哥:怎麼了嗎?
弟弟:關於你之前買的筆電，我想再跟你確認交易細節。
哥哥:上次都說過了，交易沒有問題。
->Story5_Choice1

=== Story5_Choice1 ===
+弟弟：我很擔心你的狀況，希望能和你一起確認這些細節。
->Story5_Answer.Answer1

+弟弟：這筆交易有很多不尋常的地方，你需要再仔細想清楚。
->Story5_Answer.Answer1

+弟弟：你明明知道這筆交易有問題，為什麼還要替對方隱瞞？
->Story5_Answer.Answer1

===Story5_Dialog2 ===
哥哥:我不覺得有什麼需要特別說明的，我自己心裡有數。
弟弟:如果過程很透明，應該不需要避諱討論吧?
哥哥:這是我自己的決定，你不需要過問太多。
->Story5_Choice2

=== Story5_Choice2 ===
+弟弟：我很在意你的財務安全，我希望能陪你一起核對紀錄。
->Story5_Answer.Answer2

+弟弟：既然沒有問題，我們拿出紀錄核對就能證明你的判斷正確。
->Story5_Answer.Answer2

+弟弟：你這種逃避的態度，根本不敢面對現實。
->Story5_Answer.Answer2

=== Story5_evidence1 ===
//請選擇要提出的證據：
    // 只有當 "Notebook" 完全 verify 時才會顯示這個選項
    { IsVerified("package"):
        + [包裹] -> Story5_evidence1_1
    }
    
    // 只有當 "Passbook" 完全 verify 時才會顯示這個選項
    { IsVerified("Passbook"):
        + [存摺] -> Story5_evidence1_2
    }
    
    // 只有當 "StickyNote" 完全 verify 時才會顯示這個選項
    { IsVerified("StickyNote"):
        + [便利貼] -> Story5_evidence1_3
    }
    
    // 保險起見，加入一個防呆選項（如果玩家什麼都沒 verify，避免卡死）
    + [暫時不出示證據] -> Story5_Dialog3

===Story5_evidence1_1 ===
哥哥:該付的款項我都處理好了，沒有什麼不對勁。

+弟弟：存摺上記錄著商品保留金，一般個人出清很少要求付這種錢，我們一起確認用途。
->Story5_Answer.evidence_Answer1_1
+弟弟：存摺上的保留金紀錄不合常理，一般買賣不會有這種要求。
->Story5_Answer.evidence_Answer1_1
+弟弟：匯款備註寫的保留金太離譜了，你怎麼連這麼明顯的手段都看不出來？
->Story5_Answer.evidence_Answer1_1

===Story5_evidence1_2 ===
哥哥:這台筆電規格很好，品牌標示也在機器上。

+弟弟：這個品牌很陌生，有些詐騙會用雜牌貼牌充數，我們還是再一起查一下廠商吧。
->Story5_Answer.evidence_Answer1_2
+弟弟：這種沒見過的品牌風險很大，市面上幾乎找不到官方維修資訊。
->Story5_Answer.evidence_Answer1_2
+弟弟：這根本是沒聽過的雜牌，你怎麼會買這種連製造商都查不到的東西？
->Story5_Answer.evidence_Answer1_2

===Story5_evidence1_3 ===
哥哥:外觀有使用痕跡很正常，賣家事先說明過了。

+弟弟：螢幕有明顯裂痕，我擔心有人利用瑕疵品騙取你的錢，我們還是一起評估一下，拿去退換貨吧。
->Story5_Answer.evidence_Answer1_3
+弟弟：這種程度的螢幕損壞影響正常使用，賣家事前並沒有據實告知。
->Story5_Answer.evidence_Answer1_3
+弟弟：螢幕壞成這樣你還能接受，你到底要被騙到什麼程度才甘心？
->Story5_Answer.evidence_Answer1_3

===Story5_Dialog3 ===
# TimelineSignal(17)
哥哥:這些細節我都有注意到，事情沒有你想得那麼嚴重。
弟弟:許多詐騙案例都是從這些細節開始發生的。
哥哥:我知道自己在做什麼，不需要你拿這些事質疑我。
->Story5_Choice3

=== Story5_Choice3 ===

+弟弟：我不是質疑你的能力，我是因為關心你，希望一起防範風險。
->Story5_Answer.Answer3
+弟弟：這些模式在詐騙案例中非常常見，先暫停處理比較安全。
->Story5_Answer.Answer3
+弟弟：你這種固執的態度，等錢拿不回來就知道後果。
->Story5_Answer.Answer3

===Story5_evidence2 ===
哥哥：而且物流單據也很完整，來源資訊都清楚標示在上面了。

+弟弟：貨單標註非代寄且編號是73N開頭，常出現在境外詐騙包裹上，我們或許需要再查查這個賣家是不是詐騙。
->Story5_Answer.evidence_Answer2
+弟弟：73N開頭的貨單格式並不符合賣家聲稱的國內出清，物流來源有問題。
->Story5_Answer.evidence_Answer2
+弟弟：這張73N開頭的貨單就是典型的詐騙包裹編號，你怎麼還看不出來自己上當？
->Story5_Answer.evidence_Answer2

===Story5_Dialog4 ===
哥哥:我的事情我自己會處理，你不用再插手了。
弟弟:如果這是詐騙，拖得越久越難處理。
哥哥:我說了不需要就是不需要，話題到此為止。
弟弟:...
->Story5_Choice4


=== Story5_Choice4 ===
+[我知道你現在心裡不好受，但我會陪著你，隨時能一起解決。]弟弟：我知道你現在心裡不好受，但我會陪著你，隨時能一起解決。
->Give_up
+[既然你現在不想談，我們先留存單據，你想處理時再一起處理。]弟弟：既然你現在不想談，我們先留存單據，你想處理時再一起處理。
->Give_up
+[你既然這麼講不聽，以後出大問題你自己負責，我不會再管。]弟弟：你既然這麼講不聽，以後出大問題你自己負責，我不會再管。
->Give_up


===Story5_Answer===

=Answer1
哥哥:這是我自己的消費，不需要過度解讀。
->Story5_Dialog2

=Answer2
哥哥：沒有確認的必要，整個流程都很正常，沒有問題。
->Story5_evidence1

=evidence_Answer1_1
那是因為賣家要向廠商保留現貨，這只是正常流程。
->Story5_Dialog3

=evidence_Answer1_2
哥哥:這只是小眾的平行輸入品牌，市面上比較少見。
->Story5_Dialog3

=evidence_Answer1_3
哥哥:賣家說這是運送過程的小瑕疵，在物流過程中很常見啦。
->Story5_Dialog3

=Answer3
哥哥:事情我自己會評估，不用管那麼多。
->Story5_evidence2

=evidence_Answer2
哥哥:單據只要能送到就好，編號並不影響東西本身。
->Story5_Dialog4




=== Give_up ===
.#fade.In(2.5)
希望他能早點看清這是詐騙...
希望他能早點看清這是詐騙...
...回房間休息一下吧...
.#Task.text.休息一下
.#Speaker.TargetKnot="Story6_1"
->DONE

