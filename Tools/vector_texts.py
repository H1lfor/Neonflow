from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
path=root/'Assets/Neonflow/Resources/Data/UI_TEXTS_RU.json'
data=json.loads(path.read_text(encoding='utf-8-sig'))
changes={
 'camera.wait':'Ожидание позы','songs.difficulty':'Сложность',
 'songs.title':'Выбор трека','songs.results':'Ваши результаты','songs.no_results':'Пока нет результатов','songs.no_matches':'Ничего не найдено',
 'pause.songs':'К выбору трека','results.songs':'К выбору трека','camera.title':'Калибровка','camera.framing':'Торс, плечи и поднятые руки должны быть видны целиком.',
 'camera.raise_hands':'Поднимите руки в кадр','camera.ready':'Оставайтесь в кадре 3 секунды. Продолжим автоматически.',
 'camera.hold':'Удерживайте: {seconds}','camera.search':'Покажите обе руки','hud.healing':'+{hp} HP',
 'tutorial.survive':'От 50% — лечение: +0,5 HP при 50%, +10 HP при 100%. Ниже 50% — урон.\nДоживите до конца трека. В тренировке можно играть при 0 HP.',
 'settings.interface':'Звуки интерфейса','status.tracking_lost':'Не видно плечи','status.hand_missing':'Не видно руку','status.voice_unavailable':'Микрофон недоступен',
 'error.camera_missing':'Камера не подключена','error.song_load':'Не удалось загрузить трек','hud.percent':'Совпадение: {accuracy}%',
 'tutorial.seated':'Можно играть сидя. Ноги не участвуют в оценке.','tutorial.try':'Попробовать',
 'tutorial.step.0':'Покажите позу до удара','tutorial.step.1':'Удерживайте позу','tutorial.step.2':'Точность и очки','tutorial.step.3':'Левая и правая рука',
 'tutorial.step.4':'Опущенная рука','tutorial.step.5':'Доживите до конца','tutorial.step.6':'Голосовые команды'
}
by_id={x['id']:x for x in data['strings']}
for key,value in changes.items():
    if key in by_id: by_id[key]['text']=value
    else: data['strings'].append(dict(id=key,text=value,context='Vector integration',placeholders=[]))
path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Updated the single runtime text dictionary for Vector.')
