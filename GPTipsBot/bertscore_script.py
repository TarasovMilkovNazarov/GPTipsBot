import bert_score

def calculate_bert_score(reference, candidate):
    P, R, F1 = bert_score.score(candidate, reference, lang='en', verbose=False)
    return F1.mean().item()

reference = ['This is a reference sentence.']
candidate = 'This is a candidate sentence.'

score = calculate_bert_score(reference, candidate)
print(score)
