# Fixed turn logging

กด Play แล้วเลือก TurnGameManager ใน Hierarchy:
- Fixed Turn Schedule Summary: ตารางรวมทุกเทิร์น
- Fixed Turn Schedule: รายการข้อมูลแยกวัตถุและรอบ
- Fixed Turn Log: ประวัติตาราง, การเริ่มรอบ และเหตุการณ์ที่เริ่มทำงานจริง
- Fixed Turn Log File Path: ที่อยู่ไฟล์ .log ของการเล่นครั้งนี้

Console ใช้ prefix [FixedTurn] ค้นหาได้ทันที กำหนด Log Fixed Turns To Console
และ Save Fixed Turn Log To File ได้ใน Inspector ไฟล์ UTF-8 อยู่ใน
Application.persistentDataPath/TurnLogs และแยกไฟล์แต่ละครั้งที่โหลดด่าน

เลข Round นับขึ้น 1, 2, 3 ... ไม่ใช่ CurrentTurn/TurnsLeft ที่นับถอยหลัง
เช่น maximumTurns = 7, Round 2 จะเหลือ 6 เทิร์น
กับดัก interval = 2 ถูกกำหนดรอบ 2, 4, 6; interval = 3 ถูกกำหนดรอบ 3, 6
กับดักทำงานใน Trap Phase ตอนจบรอบ หลังฝั่งศัตรู

TurnTile ยังใช้กติกาเดิม CurrentTurn >= appearTurn ดังนั้นหาก maximumTurns >=
appearTurn มันจะเริ่มลอยในรอบแรกเมื่อเปิดใช้งานตั้งแต่ต้น ระบบบันทึกกติกาปัจจุบันนี้
และไม่ได้เปลี่ยนเวลาเกิดของพื้นให้เป็นอีกแบบโดยอัตโนมัติ

ตารางเป็น snapshot ของค่าคงที่ตอนเริ่มด่าน วัตถุ inactive ก็เก็บตารางพร้อมป้ายสถานะ
แผนไม่ได้หมายความว่าทำงานสำเร็จแน่นอน: เกมอาจจบก่อน วัตถุถูกปิด/ทำลาย
หรือหนามยังยกค้าง เมื่อเริ่ม ResolveTurn จะบันทึก Execution ว่าเริ่มประมวลผล
ไม่อ้างว่าหนามถูกยกซ้ำหรือกระสุนถูกสร้างสำเร็จ
ไม่เดาเทิร์นของสวิตช์/ประตู/หีบที่ขึ้นกับผู้เล่น หรือ ArrowTrap รุ่นนับวินาที

ถ้าเพิ่มวัตถุใหม่/เปลี่ยนค่ารอบขณะเล่น เรียก RebuildFixedTurnSchedule()
หรือเมนู component > Fixed Turns > Rebuild And Log Schedule
ใช้ Print Stored Log เพื่อพิมพ์ประวัติเดิมซ้ำโดยไม่เพิ่มรายการ/เขียนไฟล์ซ้ำ

สำหรับสคริปต์อื่นที่มีกำหนดรอบตายตัว:
1. ให้ MonoBehaviour implement IFixedTurnScheduledObject
2. ระบุ ScheduledActionName และ IsScheduledOnTurn(roundNumber, maximumRounds)
3. เมื่อเริ่มทำงานจริง เรียก manager.RecordFixedTurnExecution(this, manager.RoundNumber, "รายละเอียด")

API อ่านข้อมูล: FixedTurnSchedule, FixedTurnLog, FixedTurnScheduleSummary,
FixedTurnLogFilePath และ GetScheduledObjectsForTurn(roundNumber)
บันทึกเฉพาะวัตถุใน Scene เดียวกับ TurnGameManager เพื่อไม่ปนข้อมูลจาก additive scene อื่น
