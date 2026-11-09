# Scene Manager

## ตั้งค่า / เพิ่มด่าน

1. เลือก `Assets/Script/SceneManager/SceneCatalog.asset` ใน Project
2. เพิ่มรายการใน **Levels** แล้วตั้ง ID, Title, Description และลาก Scene ลงช่อง Scene
3. ใช้ ID ใหม่ที่ไม่ซ้ำ เช่น 6, 7, 8 และอย่าเปลี่ยน ID ของด่านที่มีเซฟแล้ว
4. กด **Sync Scenes To Build Settings** ใน Inspector ของ Catalog
5. เปิด Scene เลือกด่าน ปุ่มเดิมจะรับรายการด่านตามลำดับใน Catalog อัตโนมัติ

Catalog ใช้ Preloaded Assets อ้างอิงด้วย GUID จึงไม่ต้องอยู่ใน Resources
ย้ายตำแหน่ง Asset ภายใน Unity ได้โดยเก็บไฟล์ .meta เดิมไว้
ปุ่ม Sync จะเลือก Catalog นี้เป็นตัวที่ใช้งาน ทั้งใน Editor และเกมที่ Build แล้ว

ระบบไม่ได้จำกัด 5 ด่าน ปุ่มบนบอร์ดเป็นช่องสำหรับด่านที่ยังไม่ผ่าน:
เมื่อผ่านแล้ว ด่านนั้นย้ายไป Complete และรายการถัดไปมาแทนช่องเดิม
ถ้าอยากแสดงปุ่มพร้อมกันมากขึ้น ให้เพิ่มปุ่มใน Hierarchy แล้วใส่ใน Level Buttons / Level Labels
ระบบไม่สร้างหรือจัดขนาด UI แทนผู้ทำ UI

ด่านลับตั้งใน **Secret Levels** ของ Catalog โดยกำหนด ID, Scene และ Required Completed Level
First Level ID ค่าเริ่มต้นคือ 1: เล่นด่านนี้ได้เสมอ ผ่านแล้วปลดล็อกด่านหลักทั้งหมด

## แต่ละไฟล์ทำอะไร

| ไฟล์ | หน้าที่ |
| --- | --- |
| SceneCatalog.cs | ข้อมูล Scene, ID ด่าน, ชื่อ, คำอธิบาย และเงื่อนไขด่านลับ |
| GameSceneManager.cs | ฟังก์ชันกลางสำหรับเปิด Scene / เปิดด่าน / กลับเมนู |
| MenuScreen.cs | จุดเริ่มต้นของเมนู, ปุ่มนำทาง และการกด Esc |
| MenuScreen.Settings.cs | เปิด/ปิด Settings และคืน Input ให้เมนู |
| MenuScreen.LevelSelect.cs | บอร์ดเลือกด่าน, กระดาษรายละเอียด, Complete และ Secret |
| InGameSettingsSession.cs | เปิด Settings ทับเกมแบบ Additive และกลับมาเล่นต่อ |
| LoadingScreen.cs | แสดง Loading ก่อนโหลด Scene เป้าหมายแบบ Async |
| LevelProgressStore.cs | อ่าน/เขียน JSON โดยจับคู่ด้วย ID ด่าน |
| MenuInput.cs | ป้องกัน Esc ครั้งเดียวปิดเมนูซ้อนกันหลายหน้า |
| Editor/SceneCatalogEditor.cs | ปุ่ม Sync Build Settings และตรวจรายการ Scene ก่อน Build |

ไฟล์ MenuScreen ทั้งสามเป็น `partial class` เดียวกัน จึงมี Component ใน Inspector แค่อันเดียว
การอ้างอิงปุ่มและ Panel เดิมยังอยู่ที่เดิม ไม่ต้อง Add Component ทั้งสามไฟล์

## ฟังก์ชันสำหรับปุ่ม

- `MenuScreen.OpenLevel(int levelId)` เปิดกระดาษรายละเอียดของด่านตาม ID
- `MenuScreen.OpenLevelSelect()` กลับหน้าเลือกด่าน
- `MenuScreen.OpenInGameSettings()` เปิด Settings โดยเก็บเกมที่เล่นอยู่ไว้
- `MenuScreen.CloseSettings()` ปิด Settings; ถ้าเปิดจากเกมจะกลับมาเล่นต่อ
- `MenuScreen.ReloadCurrentScene()` โหลดด่านปัจจุบันใหม่ โดยไม่ผ่านหน้า Loading

OpenLevel1 ถึง OpenLevel5 เก็บไว้รองรับปุ่มเก่า เวลาเพิ่มด่านใหม่ใช้ OpenLevel(int) ได้เลย

## Loading

Scene อยู่ที่ `Assets/Scenes/MainScene/Menus/Loading.unity`
แก้หน้าตาใน Hierarchy ของ Scene นี้ หรือแก้ `Assets/Prefabs/SceneManager/LoadScreen.prefab`
ตั้ง Min Show Time, Fade Time, Fill Speed ใน Component Loading Screen
Progress Label เป็นช่องเสริมสำหรับลาก TMP Text มาแสดงเปอร์เซ็นต์

แสดงเฉพาะตอนกด Play จากหน้า LevelSelect: ปุ่มเล่น → Loading Scene → โหลด Scene ด่านแบบ Async → ปิดหน้า Loading
ใช้ `GameSceneManager.OpenLevelFromSelect(id)` หรือ `OpenSecretFromSelect(path)` สำหรับทางเข้านี้
หน้าเมนูอื่น, กลับ LevelSelect และ Retry เปลี่ยน Scene แบบ Async โดยไม่ผ่าน Loading
ระหว่างโหลดจะรับคำสั่งเปลี่ยน Scene เพียงครั้งเดียว เพื่อป้องกันการกดซ้ำ
Settings ที่เปิดทับเกมใช้ Additive จึงไม่รีโหลดด่านและไม่ผ่านหน้า Loading

## เซฟเดิม

Editor อ่าน `Assets/Script/SceneManager/level-progress.json`
เกมที่ Build แล้วอ่าน `Application.persistentDataPath/level-progress.json`
เพิ่มด่านแล้วข้อมูลเดิมยังอยู่ ด่านใหม่เริ่มด้วย completed=false
เปลี่ยนลำดับใน Catalog ได้ เพราะ JSON ใช้ ID ด่าน ไม่ใช่ตำแหน่งใน List
ข้อมูล ID ที่เอาออกจาก Catalog ชั่วคราวจะยังเก็บไว้ใน JSON

## การแก้บั๊ก Esc

การปิด Settings ต้องปิด Overlay ด้วย เพื่อให้ Background หยุดรับคลิก
และคืน Input Action Asset ให้ EventSystem เดิม ไม่ใช่ปิดเฉพาะ Panel ด้านใน
เปิด Settings ระหว่างเกมมีสถานะ Closed / Loading / Open / Closing แยกกัน
จึงกดปิดระหว่างที่ Scene กำลังเปิดได้ และเปิดครั้งต่อไปไม่ติดสถานะเก่า

Scene Settings เดิมมี TEMPORARYMENUFIXER ที่โหลด Start โดยตรงเมื่อกด Esc
MenuScreen จะปิด Component เก่านี้ใน Scene Settings และจัดการเปิด Overlay ให้เอง
ระบบยังฟัง sceneUnloaded เพื่อเคลียร์สถานะ หาก Settings ถูกปิดจากโค้ดอื่น
