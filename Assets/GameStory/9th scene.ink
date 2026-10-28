=== Confirm ===
.#fade.In(2.5)
.#Task.text.異狀
->DONE

===Scene9_Settings===
.#door001.TargetKnot="Story9"
->DONE


===Story9 ===
.#LightingData_Day_nolight
.#Animation.timeline.9
哥哥:你一大早怎麼出現在我房間？
弟弟:剛才聽到聲音起來看，發現你不在。...這些，是你的投資的筆記嗎?
哥哥:嗯，那些筆記只是最近在跟著操作的投資策略。
->Story9_Choice1

=== Story9_Choice1 ===
+你想改善財務狀況的出發點很好，我們能一起確認這些管道是否足夠安全。
->Story9_Answer.Answer1

+我看你最近很投入在這方面，但你確定這些投資策略真的可靠嗎？
->Story9_Answer.Answer1

+就跟你說網路上教人投資的都是詐騙，你到底要被騙幾次才甘心？
->Story9_Answer.Answer1

===Story9_Dialog2 ===
弟弟:即便是專業導師帶領，市場還是有很多無法預測的風險，而且網路上的投資資訊真假難辨。
哥哥:現在市場上很多新的交易機制，只要抓對時機就能穩定獲利。
哥哥:我有做功課，資料跟對策也都整理好了，一切都在掌握中。
->Story9_Choice2

=== Story9_Choice2 ===
+現在有很多針對想投資的人設定的詐騙手法，在投入更多前，我能陪你一起評估。
->Story9_Answer.Answer2
+如果真的安全，你可以向我解釋這些策略運作的合法依據是什麼。
->Story9_Answer.Answer2
+你整理這些根本毫無根據，盲目跟單最後只會把積蓄賠光。
->Story9_Answer.Answer2

=== Story9_evidence1 ===
//請選擇要提出的證據：
    // 只有當 "Notebook" 完全 verify 時才會顯示這個選項
    { IsVerified("package"):
        + [便利貼] -> Story9_evidence1_1
    }
    
    // 只有當 "Passbook" 完全 verify 時才會顯示這個選項
    { IsVerified("Passbook"):
        + [投資相關的筆記紀錄] -> Story9_evidence1_2
    }
    
    // 只有當 "StickyNote" 完全 verify 時才會顯示這個選項
    { IsVerified("StickyNote"):
        + [投資交易網站] -> Story9_evidence1_3
    }
    
    // 保險起見，加入一個防呆選項（如果玩家什麼都沒 verify，避免卡死）
    + [暫時不出示證據] -> Story9_Dialog4

===Story9_evidence1_1===
弟弟:那這張便利貼上列出來的標的，包含了不少沒聽過的虛擬貨幣與項目。
哥哥:便利貼上是我準備購入的股票跟虛擬貨幣，都是近期熱門的項目。

+弟弟：你想多方嘗試投資很好，但有些新興項目風險極高，希望讓我當第二雙眼睛幫忙把關。
->Story9_Answer.evidence_Answer1_1
+弟弟：這些清單上的標的很多連基本的財報跟白皮書都沒有，缺乏透明度。
->Story9_Answer.evidence_Answer1_1
+弟弟：連基本背景都搞不清楚就寫一堆便利貼要買，你這跟送錢給別人有什麼兩樣？
->Story9_Answer.evidence_Answer1_1

===Story9_evidence1_2===
弟弟:但像你的那些筆記上記錄的當沖與對沖步驟，你看起來完全信任對方的指令。
哥哥:上面記錄的是跟著大戶當沖與對沖的步驟，能有效降低單一市場的風險。

+弟弟：能看到你積極規劃財務很好，但現在很多帶單詐騙，我想我們可以一起多看一眼確認安全性。
->Story9_Answer.evidence_Answer1_2
+弟弟：當沖跟對沖需要極高的專業，光靠別人的指導跟單風險非常高。
->Story9_Answer.evidence_Answer1_2
+弟弟：什麼大戶帶單都是騙人的話術，你以為靠這些筆記就能在股市賺錢？
->Story9_Answer.evidence_Answer1_2

===Story9_evidence1_3===
弟弟:還有你電腦螢幕上打開的那個交易網站，介面跟一般正規券商完全不一樣。
哥哥:電腦畫面顯示的是我們操作用的專屬平台，數據更新速度比一般券商快。

+弟弟：這類專屬平台有時會被有心人士捏造，我想我們可以一起查驗該網站是否有正規監管。
->Story9_Answer.evidence_Answer1_3
+弟弟：這種非公開且查不到官方備案的交易網站，資安與資金安全都有疑慮。
->Story9_Answer.evidence_Answer1_3
+弟弟：這根本就是詐騙集團自己架設的假網站，上面的數字全都是假的。
->Story9_Answer.evidence_Answer1_3

=== Story9_Evidence_Choice1 ===
+能看到你積極規劃財務很好，但現在很多帶單詐騙，我們一起多看一眼確認安全性。
->Story9_Answer.Answer3
+當沖跟對沖需要極高的專業，光靠別人的指導跟單風險非常高。
->Story9_Answer.Answer3
+什麼大戶帶單都是騙人的話術，你以為靠這些筆記就能在股市賺錢？
->Story9_Answer.Answer3

===Story9_Dialog3 ===
# TimelineSignal(18)
弟弟:但不管是這些資料還是交易模式，沒有經過第三方監管驗證就很難令人安心。
哥哥:我研究這些都是為了讓未來的財務狀況更穩定，這沒什麼好焦慮的。
弟弟:正因為關心你的未來，我才希望在資金投入前能多一道確認手續。
哥哥:我已經不是小孩了，知道怎麼評估風險，不需要每件事都反覆懷疑。
->Story9_Choice3

=== Story9_Choice3 ===
+最近針對想改善生活的人的金融詐騙很多，我們一起確保這項計畫跟聽起來一樣安全。
->Story9_Answer.Answer3
+只看獲利預期卻忽略平台背景，這種評估方式本身就有巨大的漏洞。
->Story9_Answer.Answer3
+你每次都說知道風險，結果還不是一直被牽著鼻子走，根本沒有判斷能力。
->Story9_Answer.Answer3

===Story9_Dialog4 ===
# TimelineSignal(18)
弟弟:但只看當前的運作順利，並不代表能完全排除隱含的風險。
哥哥:這幾天測試下來收益都很正常，你不用一直抱持著懷疑的態度。
弟弟:防範於未然總比事後虧損好，我只是希望能多幫你確認一次。
哥哥:我的事情我自己會負責，沒必要把簡單的事情複雜化。
->Story9_Choice4

=== Story9_Choice4 ===
+我很支持你想要改善生活的想法，只是希望能成為幫你把關的安全網。
->Story9_Answer.Answer4
+在還沒真正把資金全額提現前，所有的收益數據都不能當作安全的依據。
->Story9_Answer.Answer4
+你再這樣固執不聽勸，等所有錢都被騙光的時候，看你還要怎麼負責。
->Story9_Answer.Answer4


===Story9_Answer===

=Answer1
這些都是有專業導師帶領的，步驟非常明確，不用過度擔心。
->Story9_Dialog2

=Answer2
哥哥:這些資料我都反覆核對過了，事情沒有你想得那麼複雜。
->Story9_evidence1

=evidence_Answer1_1
哥哥:機會稍縱即逝，提早規劃好清單，等到市場時間點一到才能立刻進場。
->Story9_Dialog4

=evidence_Answer1_2
哥哥:這套操作模式有固定指標可以參考，只要照著執行就不會有問題。
->Story9_Dialog4

=evidence_Answer1_3
哥哥:只要平台能順利連線並顯示收益狀況就行，網站形式並不影響運作。
->Story9_Dialog4

=Answer3
哥哥:事情都有按照步驟在進行，我的判斷沒有問題。
->Story9_Dialog4

=Answer4
哥哥：你的關心我知道，但我心裡有數。早餐我買回來了，收拾一下出來客廳吃。
->having_breakfast

=== having_breakfast===
.#LightingData_Day_light
.#fade.In(2.5)
.#Task.text.吃早餐
.#Food.SetActive.true
.#Old_Brother.TargetKnot="check_ending1"
->DONE

