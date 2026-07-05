-- Domaines "fourre-tout" (nom = <cours> - <niveau>) qui contiennent ENCORE des visées
SELECT c.code_cours, d.nom, count(v.id_visee) AS nb
FROM domaine d
JOIN cours_niveau cn ON cn.id_cours_niveau=d.id_cours_niveau_fk
JOIN cours c ON c.id_cours=cn.id_cours_fk
JOIN visees v ON v.id_domaine_fk=d.id_dom
WHERE d.nom ~ ' - (1re|2e|3e|4e|5e|6e|1ère)? ?(primaire|secondaire)$'
   OR d.nom ~ ' - (P|S|M)[0-9]$'
   OR d.nom LIKE 'ECA - P%'
GROUP BY c.code_cours, d.nom
ORDER BY c.code_cours, d.nom;
